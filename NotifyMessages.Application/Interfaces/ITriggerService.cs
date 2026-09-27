using NotifyMessages.Application.DTOs;

namespace NotifyMessages.Application.Interfaces;

public interface ITriggerService
{
    // Gatilhos do tenant devidos em utcNow (hora local do tenant) e ainda sem lote de conector para esse dia.
    Task<IReadOnlyList<DueTriggerDto>> GetDueAsync(int tenantId, DateTime utcNow, CancellationToken ct = default);

    // Administração (todos os tenants). Create/Update lançam RequestValidationException ou TemplateNotAvailableException.
    Task<IReadOnlyList<TriggerDto>> ListAsync(int? tenantId, CancellationToken ct = default);
    Task<TriggerDto?> GetAsync(int triggerId, CancellationToken ct = default);
    Task<TriggerDto> CreateAsync(TriggerUpsertDto request, string actingUser, CancellationToken ct = default);
    Task<TriggerDto?> UpdateAsync(int triggerId, TriggerUpsertDto request, string actingUser, CancellationToken ct = default);
}
