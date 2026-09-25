using NotifyMessages.Application.DTOs;

namespace NotifyMessages.Application.Interfaces;

public interface IBatchService
{
    // Cria o lote e os seus envios. Lança DuplicateBatchException (lote de conector repetido),
    // RequestValidationException (gatilho/pedido inválido) ou TemplateNotAvailableException.
    Task<BatchDto> CreateAsync(int tenantId, BatchCreateDto request, string? createdBy = null, CancellationToken ct = default);

    // Estado atual; null se não existir ou for de outro tenant.
    Task<BatchDto?> GetAsync(int tenantId, long batchId, CancellationToken ct = default);
}
