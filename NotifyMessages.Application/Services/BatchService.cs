using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Exceptions;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Application.Validators;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.Services;

// Lotes de envios (POST/GET /api/v1/batches). Cada item vira uma linha de MESSAGE_DISPATCH:
// Held (à espera de aprovação), Queued (aprovado) ou Suppressed. Duplicados e inválidos não criam linha.
public class BatchService : IBatchService
{
    public const int MaxItems = 10_000;
    private const int MaxDetailEntries = 1_000;
    private const int SqlChunk = 1_000;

    // Detalhes legíveis na BD e na UI (sem escapar acentos: "inválido", não "inválido").
    private static readonly JsonSerializerOptions DetailsJson = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
    };

    private readonly IAppDbContext _context;

    public BatchService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<BatchDto> CreateAsync(int tenantId, BatchCreateDto request, string? createdBy = null, CancellationToken ct = default)
    {
        // 1. Origem: gatilho do tenant (conector) ou template direto (API)
        int templateId;
        bool requiresApproval;
        BatchSource source;
        if (request.TriggerId is int triggerId)
        {
            var trigger = await _context.MessageTriggers.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == triggerId && t.TenantId == tenantId && t.IsActive, ct)
                ?? throw new RequestValidationException("Gatilho inválido", $"Gatilho {triggerId} não existe, está inativo ou não é deste tenant.");
            if (request.RunDate is null)
            {
                throw new RequestValidationException("RunDate obrigatório", "Um lote de gatilho tem de indicar o RunDate devolvido por /triggers/due.");
            }
            templateId = trigger.TemplateId;
            requiresApproval = trigger.RequiresApproval;
            source = BatchSource.Connector;

            var existente = await FindConnectorBatchAsync(triggerId, request.RunDate.Value, ct);
            if (existente.HasValue)
            {
                throw new DuplicateBatchException(existente.Value);
            }
        }
        else
        {
            templateId = request.TemplateId
                ?? throw new RequestValidationException("TemplateId obrigatório", "Um lote sem gatilho tem de indicar o TemplateId.");
            requiresApproval = false;
            source = BatchSource.Api;
        }

        if (request.Items.Count > MaxItems)
        {
            throw new RequestValidationException("Lote demasiado grande", $"Máximo de {MaxItems} itens por lote.");
        }

        // 2. O template tem de ser do tenant ou partilhado
        bool templateDisponivel = await _context.Templates.AnyAsync(t =>
            t.Id == templateId && (t.TenantId == tenantId || t.TenantId == null), ct);
        if (!templateDisponivel)
        {
            throw new TemplateNotAvailableException(templateId);
        }

        // 3. Classificar os itens: inválidos, duplicados (no lote ou já enviados antes), suprimidos, aceites
        var rejeitados = new List<object>();
        var duplicados = new List<string>();
        var validos = new List<(BatchItemDto Item, string Key, string Contact)>();
        var chavesNoLote = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in request.Items)
        {
            string key = item.ExternalKey?.Trim() ?? string.Empty;
            string? motivo = ValidarItem(item, key);
            if (motivo != null)
            {
                rejeitados.Add(new { externalKey = key, reason = motivo });
                continue;
            }
            if (!chavesNoLote.Add(key))
            {
                duplicados.Add(key);
                continue;
            }
            validos.Add((item, key, ContactNormalizer.Normalize(item.RecipientContact)));
        }

        var jaExistentes = await ExistingKeysAsync(tenantId, validos.Select(v => v.Key).ToList(), ct);
        var suprimidos = await SuppressedContactsAsync(tenantId, validos.Select(v => v.Contact).Distinct().ToList(), ct);

        var batch = new DispatchBatch
        {
            TenantId = tenantId,
            TriggerId = request.TriggerId,
            Source = source,
            RunDate = request.RunDate,
            ReferenceDate = request.ReferenceDate,
            Received = request.Items.Count,
            CreatedBy = createdBy
        };

        foreach (var (item, key, contact) in validos)
        {
            if (jaExistentes.Contains(key))
            {
                duplicados.Add(key);
                continue;
            }

            bool suprimido = suprimidos.Contains(contact);
            var dispatch = new MessageDispatch
            {
                TenantId = tenantId,
                TemplateId = templateId,
                TriggerId = request.TriggerId,
                Batch = batch,
                ExternalKey = key,
                IdempotencyKey = IdempotencyKeys.ForExternalKey(tenantId, key),
                RecipientName = item.RecipientName.Trim(),
                RecipientContact = item.RecipientContact.Trim(),
                ScheduledAt = item.ScheduledAt,
                CurrentStatus = suprimido ? DispatchStatus.Suppressed
                    : requiresApproval ? DispatchStatus.Held
                    : DispatchStatus.Queued
            };
            dispatch.SetContextData(item.BusinessData ?? new Dictionary<string, string>());
            _context.MessageDispatches.Add(dispatch);

            if (suprimido) batch.Suppressed++;
            else batch.Accepted++;
        }

        batch.Duplicates = duplicados.Count;
        batch.Rejected = rejeitados.Count;

        // 4. Estado: à espera de aprovação, aprovado automaticamente, ou já concluído (nada a enviar)
        if (batch.Accepted == 0)
        {
            batch.Status = BatchStatus.Completed;
            batch.CompletedAt = DateTime.UtcNow;
        }
        else if (requiresApproval)
        {
            batch.Status = BatchStatus.PendingApproval;
        }
        else
        {
            batch.Status = BatchStatus.Approved;
            batch.ApprovedAt = DateTime.UtcNow;
            batch.ApprovedBy = "auto";
        }

        batch.Details = JsonSerializer.Serialize(new
        {
            excluded = request.Excluded.Take(MaxDetailEntries),
            excludedTotal = request.Excluded.Count,
            warnings = request.Warnings,
            rejected = rejeitados.Take(MaxDetailEntries),
            duplicates = duplicados.Take(MaxDetailEntries)
        }, DetailsJson);

        _context.DispatchBatches.Add(batch);
        try
        {
            // Lote e envios numa só operação (uma transação)
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (request.TriggerId is int t && request.RunDate is DateOnly d)
        {
            // Dois pedidos do mesmo gatilho/dia em simultâneo: o índice único barra o segundo
            var existente = await FindConnectorBatchAsync(t, d, ct);
            if (existente.HasValue)
            {
                throw new DuplicateBatchException(existente.Value);
            }
            throw;
        }

        return (await GetAsync(tenantId, batch.Id, ct))!;
    }

    public async Task<BatchDto?> GetAsync(int tenantId, long batchId, CancellationToken ct = default)
    {
        var batch = await _context.DispatchBatches.FirstOrDefaultAsync(b => b.Id == batchId && b.TenantId == tenantId, ct);
        if (batch is null)
        {
            return null;
        }

        var progresso = await _context.MessageDispatches
            .AsNoTracking()
            .Where(m => m.BatchId == batchId)
            .GroupBy(m => m.CurrentStatus)
            .Select(g => new { Status = g.Key, Total = g.Count() })
            .ToListAsync(ct);

        // Aprovado e sem nada por enviar: concluído
        bool porEnviar = progresso.Any(p => p.Status is DispatchStatus.Queued or DispatchStatus.Processing or DispatchStatus.Held);
        if ((batch.Status is BatchStatus.Approved or BatchStatus.Dispatching) && !porEnviar)
        {
            batch.Status = BatchStatus.Completed;
            batch.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        return new BatchDto
        {
            Id = batch.Id,
            TenantId = batch.TenantId,
            TriggerId = batch.TriggerId,
            Source = batch.Source,
            RunDate = batch.RunDate,
            ReferenceDate = batch.ReferenceDate,
            Status = batch.Status,
            Received = batch.Received,
            Accepted = batch.Accepted,
            Duplicates = batch.Duplicates,
            Suppressed = batch.Suppressed,
            Rejected = batch.Rejected,
            Progress = progresso.ToDictionary(p => p.Status.ToString(), p => p.Total),
            Details = batch.Details,
            CreatedAt = batch.CreatedAt,
            ApprovedAt = batch.ApprovedAt,
            CompletedAt = batch.CompletedAt
        };
    }

    private static string? ValidarItem(BatchItemDto item, string key)
    {
        if (key.Length == 0) return "ExternalKey em falta";
        if (key.Length > 150) return "ExternalKey com mais de 150 caracteres";
        if (string.IsNullOrWhiteSpace(item.RecipientName)) return "RecipientName em falta";
        if (item.RecipientName.Trim().Length > 100) return "RecipientName com mais de 100 caracteres";
        if (!ContactRules.IsValid(item.RecipientContact?.Trim())) return "RecipientContact inválido (e-mail ou telefone)";
        return null;
    }

    private async Task<long?> FindConnectorBatchAsync(int triggerId, DateOnly runDate, CancellationToken ct)
        => await _context.DispatchBatches.AsNoTracking()
            .Where(b => b.TriggerId == triggerId && b.RunDate == runDate && b.Source == BatchSource.Connector)
            .Select(b => (long?)b.Id)
            .FirstOrDefaultAsync(ct);

    // Consultas em blocos: o SQL Server aceita no máximo ~2100 parâmetros por comando.
    private async Task<HashSet<string>> ExistingKeysAsync(int tenantId, List<string> keys, CancellationToken ct)
    {
        var existentes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chunk in keys.Chunk(SqlChunk))
        {
            var encontrados = await _context.MessageDispatches.AsNoTracking()
                .Where(m => m.TenantId == tenantId && m.ExternalKey != null && chunk.Contains(m.ExternalKey))
                .Select(m => m.ExternalKey!)
                .ToListAsync(ct);
            existentes.UnionWith(encontrados);
        }
        return existentes;
    }

    private async Task<HashSet<string>> SuppressedContactsAsync(int tenantId, List<string> contacts, CancellationToken ct)
    {
        var suprimidos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chunk in contacts.Chunk(SqlChunk))
        {
            var encontrados = await _context.Suppressions.AsNoTracking()
                .Where(s => s.TenantId == tenantId && chunk.Contains(s.Contact))
                .Select(s => s.Contact)
                .ToListAsync(ct);
            suprimidos.UnionWith(encontrados);
        }
        return suprimidos;
    }
}
