using Microsoft.EntityFrameworkCore;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Interfaces;
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

        var candidatos = (await _context.MessageTriggers
                .AsNoTracking()
                .Where(t => t.TenantId == tenantId && t.IsActive && t.Code != FileCode)
                .ToListAsync(ct))
            .Where(t => IsDue(t.StartDate, t.ScheduleDay, t.ScheduleTime, hoje, agora))
            .ToList();

        if (candidatos.Count == 0)
        {
            return [];
        }

        var ids = candidatos.Select(t => t.Id).ToList();
        var jaCorreram = await _context.DispatchBatches
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.Source == BatchSource.Connector && b.RunDate == hoje
                        && b.TriggerId != null && ids.Contains(b.TriggerId.Value))
            .Select(b => b.TriggerId!.Value)
            .ToListAsync(ct);

        return candidatos
            .Where(t => !jaCorreram.Contains(t.Id))
            .OrderBy(t => t.ScheduleTime).ThenBy(t => t.Id)
            .Select(t => new DueTriggerDto
            {
                TriggerId = t.Id,
                Code = t.Code,
                Name = t.Name,
                TemplateId = t.TemplateId,
                RunDate = hoje,
                Parameters = t.Parameters,
                RequiresApproval = t.RequiresApproval
            })
            .ToList();
    }

    // Data de início, dia do mês (nulo = todos) e hora local já passada.
    internal static bool IsDue(DateOnly? startDate, byte? scheduleDay, TimeOnly scheduleTime, DateOnly hoje, TimeOnly agora)
        => (startDate is null || hoje >= startDate.Value)
           && (scheduleDay is null || hoje.Day == scheduleDay.Value)
           && agora >= scheduleTime;
}
