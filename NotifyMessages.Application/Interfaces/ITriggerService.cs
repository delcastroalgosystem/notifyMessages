using NotifyMessages.Application.DTOs;

namespace NotifyMessages.Application.Interfaces;

public interface ITriggerService
{
    // Gatilhos do tenant devidos em utcNow (hora local do tenant) e ainda sem lote de conector para esse dia.
    Task<IReadOnlyList<DueTriggerDto>> GetDueAsync(int tenantId, DateTime utcNow, CancellationToken ct = default);
}
