using Microsoft.EntityFrameworkCore;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Exceptions;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.Services;

public class TriggerService : ITriggerService
{
    // Gatilhos de carga manual: nunca são "devidos" para um conector.
    public const string FileCode = "FILE";

    private readonly IAppDbContext _context;

    public TriggerService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DueTriggerDto>> GetDueAsync(int tenantId, DateTime utcNow, CancellationToken ct = default)
    {
        var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId && t.Active, ct);
        if (tenant is null)
        {
            return [];
        }

        DateTime local = TenantClock.ToLocal(utcNow, tenant.TimeZone);
        var hoje = DateOnly.FromDateTime(local);
        var agora = TimeOnly.FromDateTime(local);

        var ativos = await _context.MessageTriggers
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.IsActive && t.Code != FileCode)
            .ToListAsync(ct);

        // Diários/mensais: dia e hora da agenda, um lote por dia
        var candidatos = ativos
            .Where(t => t.IntervalMinutes is null && IsDue(t.StartDate, t.ScheduleDay, t.ScheduleTime, hoje, agora))
            .ToList();
        var jaCorreram = new List<int>();
        if (candidatos.Count > 0)
        {
            var ids = candidatos.Select(t => t.Id).ToList();
            jaCorreram = await _context.DispatchBatches
                .AsNoTracking()
                .Where(b => b.TenantId == tenantId && b.Source == BatchSource.Connector && b.RunDate == hoje && b.RunAt == null
                            && b.TriggerId != null && ids.Contains(b.TriggerId.Value))
                .Select(b => b.TriggerId!.Value)
                .ToListAsync(ct);
        }

        // De intervalo: passados IntervalMinutes desde a última execução entregue
        var intervalo = ativos
            .Where(t => t.IntervalMinutes is int n && IsIntervalDue(t.StartDate, t.LastRunAt, n, hoje, utcNow))
            .ToList();

        return candidatos
            .Where(t => !jaCorreram.Contains(t.Id))
            .OrderBy(t => t.ScheduleTime).ThenBy(t => t.Id)
            .Concat(intervalo.OrderBy(t => t.Id))
            .Select(t => new DueTriggerDto
            {
                TriggerId = t.Id,
                Code = t.Code,
                Name = t.Name,
                TemplateId = t.TemplateId,
                RunDate = hoje,
                Parameters = t.Parameters,
                RequiresApproval = t.RequiresApproval,
                IntervalMinutes = t.IntervalMinutes,
                StartDate = t.StartDate
            })
            .ToList();
    }

    internal static bool IsIntervalDue(DateOnly? startDate, DateTime? lastRunAtUtc, int intervalMinutes, DateOnly hoje, DateTime utcNow)
        => (startDate is null || hoje >= startDate.Value)
           && (lastRunAtUtc is null || utcNow - lastRunAtUtc.Value >= TimeSpan.FromMinutes(intervalMinutes));

    public async Task<IReadOnlyList<TriggerDto>> ListAsync(int? tenantId, CancellationToken ct = default)
        => (await _context.MessageTriggers.AsNoTracking()
                .Where(t => tenantId == null || t.TenantId == tenantId)
                .OrderBy(t => t.TenantId).ThenBy(t => t.Name)
                .ToListAsync(ct))
            .Select(ToDto).ToList();

    public async Task<TriggerDto?> GetAsync(int triggerId, CancellationToken ct = default)
    {
        var trigger = await _context.MessageTriggers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == triggerId, ct);
        return trigger is null ? null : ToDto(trigger);
    }

    public async Task<TriggerDto> CreateAsync(TriggerUpsertDto request, string actingUser, CancellationToken ct = default)
    {
        if (!await _context.Tenants.AnyAsync(t => t.Id == request.TenantId, ct))
        {
            throw new RequestValidationException("Tenant inválido", $"Tenant {request.TenantId} não existe.");
        }

        var trigger = new MessageTrigger { TenantId = request.TenantId, CreatedBy = actingUser, CreatedAt = DateTime.UtcNow };
        await ApplyAsync(trigger, request, ct);
        _context.MessageTriggers.Add(trigger);
        await _context.SaveChangesAsync(ct);
        return ToDto(trigger);
    }

    public async Task<TriggerDto?> UpdateAsync(int triggerId, TriggerUpsertDto request, string actingUser, CancellationToken ct = default)
    {
        var trigger = await _context.MessageTriggers.FirstOrDefaultAsync(t => t.Id == triggerId, ct);
        if (trigger is null)
        {
            return null;
        }

        await ApplyAsync(trigger, request, ct);
        trigger.UpdatedBy = actingUser;
        trigger.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return ToDto(trigger);
    }

    private async Task ApplyAsync(MessageTrigger trigger, TriggerUpsertDto request, CancellationToken ct)
    {
        string code = request.Code?.Trim() ?? string.Empty;
        string name = request.Name?.Trim() ?? string.Empty;
        if (code.Length is 0 or > 50) throw new RequestValidationException("Código inválido", "O código do gatilho é obrigatório (máx. 50 caracteres).");
        if (name.Length is 0 or > 100) throw new RequestValidationException("Nome inválido", "O nome do gatilho é obrigatório (máx. 100 caracteres).");
        if (request.ScheduleDay is < 1 or > 28) throw new RequestValidationException("Dia inválido", "O dia de execução tem de estar entre 1 e 28 (ou vazio = todos os dias).");
        if (request.IntervalMinutes is < 5 or > 1440) throw new RequestValidationException("Intervalo inválido", "O intervalo tem de estar entre 5 e 1440 minutos (ou vazio = pela agenda).");
        if (request.IntervalMinutes is not null && code == FileCode) throw new RequestValidationException("Intervalo inválido", "Um gatilho de carga manual (FILE) não tem intervalo.");
        if (!string.IsNullOrWhiteSpace(request.Parameters))
        {
            try { using var _ = System.Text.Json.JsonDocument.Parse(request.Parameters); }
            catch (System.Text.Json.JsonException) { throw new RequestValidationException("Parâmetros inválidos", "Os parâmetros do gatilho têm de ser JSON válido."); }
        }

        // O template tem de ser do tenant do gatilho ou partilhado
        bool templateDisponivel = await _context.Templates.AnyAsync(t =>
            t.Id == request.TemplateId && (t.TenantId == trigger.TenantId || t.TenantId == null), ct);
        if (!templateDisponivel)
        {
            throw new TemplateNotAvailableException(request.TemplateId);
        }

        bool nomeRepetido = await _context.MessageTriggers.AnyAsync(t =>
            t.TenantId == trigger.TenantId && t.Name == name && t.Id != trigger.Id, ct);
        if (nomeRepetido)
        {
            throw new RequestValidationException("Nome repetido", $"Já existe um gatilho \"{name}\" neste tenant.");
        }

        trigger.Code = code;
        trigger.Name = name;
        trigger.TemplateId = request.TemplateId;
        trigger.ScheduleDay = request.ScheduleDay;
        trigger.ScheduleTime = request.ScheduleTime;
        trigger.IntervalMinutes = request.IntervalMinutes;
        trigger.StartDate = request.StartDate;
        trigger.Parameters = string.IsNullOrWhiteSpace(request.Parameters) ? null : request.Parameters;
        trigger.RequiresApproval = request.RequiresApproval;
        trigger.IsActive = request.IsActive;
    }

    private static TriggerDto ToDto(MessageTrigger t) => new()
    {
        Id = t.Id,
        TenantId = t.TenantId,
        Code = t.Code,
        Name = t.Name,
        TemplateId = t.TemplateId,
        ScheduleDay = t.ScheduleDay,
        ScheduleTime = t.ScheduleTime,
        IntervalMinutes = t.IntervalMinutes,
        LastRunAt = t.LastRunAt,
        StartDate = t.StartDate,
        Parameters = t.Parameters,
        RequiresApproval = t.RequiresApproval,
        IsActive = t.IsActive,
        CreatedBy = t.CreatedBy,
        CreatedAt = t.CreatedAt,
        UpdatedBy = t.UpdatedBy,
        UpdatedAt = t.UpdatedAt
    };

    // Data de início, dia do mês (nulo = todos) e hora local já passada.
    internal static bool IsDue(DateOnly? startDate, byte? scheduleDay, TimeOnly scheduleTime, DateOnly hoje, TimeOnly agora)
        => (startDate is null || hoje >= startDate.Value)
           && (scheduleDay is null || hoje.Day == scheduleDay.Value)
           && agora >= scheduleTime;
}
