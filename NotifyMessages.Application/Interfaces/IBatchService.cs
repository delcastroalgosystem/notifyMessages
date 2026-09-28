using NotifyMessages.Application.DTOs;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.Interfaces;

public interface IBatchService
{
    // Cria o lote e os seus envios. Lança DuplicateBatchException (lote de conector repetido),
    // RequestValidationException (gatilho/pedido inválido) ou TemplateNotAvailableException.
    // Nulo: execução de um gatilho de intervalo sem nada de novo (não fica lote).
    Task<BatchDto?> CreateAsync(int tenantId, BatchCreateDto request, string? createdBy = null, CancellationToken ct = default);

    // Estado atual; null se não existir ou (com tenantId) for de outro tenant. tenantId = null: administração.
    Task<BatchDto?> GetAsync(int? tenantId, long batchId, CancellationToken ct = default);

    // --- Administração ---
    Task<BatchDto> CreateFromFileAsync(int tenantId, int? triggerId, int? templateId, string fileName,
        ParsedBatchFile parsed, string actingUser, CancellationToken ct = default);
    Task<PagedResultDto<BatchDto>> ListAsync(BatchQueryDto query, CancellationToken ct = default);
    Task<PagedResultDto<DispatchListItemDto>?> ItemsAsync(long batchId, DispatchStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<BatchDto?> ApproveAsync(long batchId, string actingUser, CancellationToken ct = default);
    Task<BatchDto?> CancelAsync(long batchId, string actingUser, CancellationToken ct = default);
}
