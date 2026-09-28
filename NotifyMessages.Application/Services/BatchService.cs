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

    // Nulo: execução de um gatilho de intervalo sem nada de novo (tudo duplicado ou vazio) — não fica lote.
    public Task<BatchDto?> CreateAsync(int tenantId, BatchCreateDto request, string? createdBy = null, CancellationToken ct = default)
        => CreateCoreAsync(tenantId, request, fileName: null, createdBy, ct);

    // Lote a partir de um ficheiro CSV/Excel (API de administração): sempre à espera de aprovação.
    // Linhas sem chave recebem FILE:{gatilho ou template}:{data}:{contacto} — carregar o mesmo ficheiro no mesmo dia não duplica.
    public async Task<BatchDto> CreateFromFileAsync(int tenantId, int? triggerId, int? templateId, string fileName,
        ParsedBatchFile parsed, string actingUser, CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        string origem = triggerId is int t ? $"G{t}" : $"T{templateId}";
        foreach (var item in parsed.Items.Where(i => string.IsNullOrWhiteSpace(i.ExternalKey)))
        {
            item.ExternalKey = $"FILE:{origem}:{hoje:yyyyMMdd}:{ContactNormalizer.Normalize(item.RecipientContact ?? string.Empty)}";
        }

        var request = new BatchCreateDto
        {
            TriggerId = triggerId,
            TemplateId = templateId,
            RunDate = hoje,
            Items = parsed.Items,
            Warnings = parsed.Errors
        };
        // Um ficheiro nunca é de gatilho de intervalo: há sempre lote
        return (await CreateCoreAsync(tenantId, request, fileName, actingUser, ct))!;
    }

    private async Task<BatchDto?> CreateCoreAsync(int tenantId, BatchCreateDto request, string? fileName, string? createdBy, CancellationToken ct)
    {
        bool fromFile = fileName != null;

        // 1. Origem: gatilho do tenant (conector ou ficheiro) ou template direto (API ou ficheiro)
        int templateId;
        bool requiresApproval;
        BatchSource source;
        MessageTrigger? gatilhoIntervalo = null;
        if (request.TriggerId is int triggerId)
        {
            var trigger = await _context.MessageTriggers
                .FirstOrDefaultAsync(t => t.Id == triggerId && t.TenantId == tenantId && t.IsActive, ct)
                ?? throw new RequestValidationException("Gatilho inválido", $"Gatilho {triggerId} não existe, está inativo ou não é deste tenant.");
            templateId = trigger.TemplateId;
            // Um ficheiro é sempre revisto antes de enviar
            requiresApproval = fromFile || trigger.RequiresApproval;
            source = fromFile ? BatchSource.File : BatchSource.Connector;

            if (!fromFile)
            {
                if (request.RunDate is null)
                {
                    throw new RequestValidationException("RunDate obrigatório", "Um lote de gatilho tem de indicar o RunDate devolvido por /triggers/due.");
                }
                if (trigger.IntervalMinutes is not null)
                {
                    // Gatilho de intervalo: vários lotes por dia; marca a execução (mesmo que não fique lote)
                    gatilhoIntervalo = trigger;
                    trigger.LastRunAt = DateTime.UtcNow;
                }
            }
            if (!fromFile && gatilhoIntervalo is null)
            {
                var existente = await FindConnectorBatchAsync(triggerId, request.RunDate!.Value, ct);
                if (existente.HasValue)
                {
                    throw new DuplicateBatchException(existente.Value);
                }
            }
        }
        else
        {
            templateId = request.TemplateId
                ?? throw new RequestValidationException("TemplateId obrigatório", "Um lote sem gatilho tem de indicar o TemplateId.");
            requiresApproval = fromFile;
            source = fromFile ? BatchSource.File : BatchSource.Api;
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
            RunAt = gatilhoIntervalo?.LastRunAt,
            ReferenceDate = request.ReferenceDate,
            Received = request.Items.Count,
            FileName = fileName,
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

        // Gatilho de intervalo sem envios novos (vazio, ou tudo já entregue antes): não fica lote, só a execução.
        // Sem isto, cada execução de 15 em 15 minutos deixaria um lote vazio ou de duplicados.
        if (gatilhoIntervalo is not null && batch.Accepted == 0 && batch.Suppressed == 0)
        {
            await _context.SaveChangesAsync(ct);
            return null;
        }

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
        catch (DbUpdateException) when (!fromFile && request.TriggerId is int t && request.RunDate is DateOnly d)
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

    // tenantId = null: administração (qualquer tenant).
    public async Task<BatchDto?> GetAsync(int? tenantId, long batchId, CancellationToken ct = default)
    {
        var batch = await _context.DispatchBatches.FirstOrDefaultAsync(b => b.Id == batchId && (tenantId == null || b.TenantId == tenantId), ct);
        if (batch is null)
        {
            return null;
        }

        var progresso = (await ProgressAsync([batchId], ct)).GetValueOrDefault(batchId) ?? [];

        // Aprovado e sem nada por enviar: concluído
        bool porEnviar = progresso.Keys.Any(s => s is nameof(DispatchStatus.Queued) or nameof(DispatchStatus.Processing) or nameof(DispatchStatus.Held));
        if ((batch.Status is BatchStatus.Approved or BatchStatus.Dispatching) && !porEnviar)
        {
            batch.Status = BatchStatus.Completed;
            batch.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        return ToDto(batch, progresso);
    }

    public async Task<PagedResultDto<BatchDto>> ListAsync(BatchQueryDto query, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging(query.Page, query.PageSize);
        var q = _context.DispatchBatches.AsNoTracking()
            .Where(b => query.TenantId == null || b.TenantId == query.TenantId)
            .Where(b => query.TriggerId == null || b.TriggerId == query.TriggerId)
            .Where(b => query.Status == null || b.Status == query.Status)
            .Where(b => query.From == null || b.CreatedAt >= query.From)
            .Where(b => query.To == null || b.CreatedAt <= query.To);

        int total = await q.CountAsync(ct);
        var batches = await q.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var progresso = await ProgressAsync(batches.Select(b => b.Id).ToList(), ct);

        return new PagedResultDto<BatchDto>
        {
            Items = batches.Select(b => ToDto(b, progresso.GetValueOrDefault(b.Id) ?? [])).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResultDto<DispatchListItemDto>?> ItemsAsync(long batchId, DispatchStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        if (!await _context.DispatchBatches.AnyAsync(b => b.Id == batchId, ct))
        {
            return null;
        }
        (page, pageSize) = Paging(page, pageSize);
        var q = _context.MessageDispatches.AsNoTracking()
            .Where(m => m.BatchId == batchId && (status == null || m.CurrentStatus == status));
        int total = await q.CountAsync(ct);
        var itens = await q.OrderBy(m => m.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(DispatchQueryService.ToListItem).ToListAsync(ct);
        return new PagedResultDto<DispatchListItemDto> { Items = itens, Total = total, Page = page, PageSize = pageSize };
    }

    // PendingApproval → Approved: os envios Held passam a Queued e o Worker envia-os.
    public async Task<BatchDto?> ApproveAsync(long batchId, string actingUser, CancellationToken ct = default)
    {
        var batch = await _context.DispatchBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null)
        {
            return null;
        }
        if (batch.Status != BatchStatus.PendingApproval)
        {
            throw new RequestValidationException("Estado inválido", $"Só um lote à espera de aprovação pode ser aprovado (este está {batch.Status}).");
        }

        var retidos = await _context.MessageDispatches.Where(m => m.BatchId == batchId && m.CurrentStatus == DispatchStatus.Held).ToListAsync(ct);
        foreach (var m in retidos)
        {
            m.CurrentStatus = DispatchStatus.Queued;
        }
        batch.Status = retidos.Count > 0 ? BatchStatus.Approved : BatchStatus.Completed;
        batch.ApprovedBy = actingUser;
        batch.ApprovedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return await GetAsync(null, batchId, ct);
    }

    // Cancela o que ainda não foi enviado (Held/Queued → Canceled). Um lote concluído ou cancelado não muda.
    public async Task<BatchDto?> CancelAsync(long batchId, string actingUser, CancellationToken ct = default)
    {
        var batch = await _context.DispatchBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null)
        {
            return null;
        }
        if (batch.Status is BatchStatus.Completed or BatchStatus.Canceled)
        {
            throw new RequestValidationException("Estado inválido", $"Um lote {batch.Status} já não pode ser cancelado.");
        }

        var porEnviar = await _context.MessageDispatches
            .Where(m => m.BatchId == batchId && (m.CurrentStatus == DispatchStatus.Held || m.CurrentStatus == DispatchStatus.Queued))
            .ToListAsync(ct);
        foreach (var m in porEnviar)
        {
            m.CurrentStatus = DispatchStatus.Canceled;
            m.ErrorLog = $"Lote cancelado por {actingUser}";
        }
        batch.Status = BatchStatus.Canceled;
        batch.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return await GetAsync(null, batchId, ct);
    }

    private async Task<Dictionary<long, Dictionary<string, int>>> ProgressAsync(List<long> batchIds, CancellationToken ct)
    {
        if (batchIds.Count == 0)
        {
            return [];
        }
        var linhas = await _context.MessageDispatches.AsNoTracking()
            .Where(m => m.BatchId != null && batchIds.Contains(m.BatchId.Value))
            .GroupBy(m => new { BatchId = m.BatchId!.Value, m.CurrentStatus })
            .Select(g => new { g.Key.BatchId, g.Key.CurrentStatus, Total = g.Count() })
            .ToListAsync(ct);
        return linhas.GroupBy(l => l.BatchId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(l => l.CurrentStatus.ToString(), l => l.Total));
    }

    internal static (int Page, int PageSize) Paging(int page, int pageSize)
        => (Math.Max(1, page), Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 500));

    private static BatchDto ToDto(DispatchBatch batch, Dictionary<string, int> progresso)
    {
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
            Progress = progresso,
            Details = batch.Details,
            FileName = batch.FileName,
            CreatedBy = batch.CreatedBy,
            ApprovedBy = batch.ApprovedBy,
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
