using NotifyMessages.Application.DTOs;

namespace NotifyMessages.Application.Interfaces;

public interface IDispatchService
{
    /// <summary>Enfileira o envio e devolve o Id do MessageDispatch criado.</summary>
    Task<long> EnqueueMessageAsync(DispatchRequestDto request);

    /// <summary>Estado atual de um envio do tenant; null se não existir ou pertencer a outro tenant.</summary>
    Task<DispatchStatusDto?> GetStatusAsync(long dispatchId, int tenantId, CancellationToken ct = default);
}
