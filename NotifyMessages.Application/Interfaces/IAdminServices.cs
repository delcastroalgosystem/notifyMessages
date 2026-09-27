using NotifyMessages.Application.DTOs;

namespace NotifyMessages.Application.Interfaces;

public interface IDispatchQueryService
{
    Task<DispatchHistoryDto> SearchAsync(DispatchQueryDto query, CancellationToken ct = default);
}

public interface ISuppressionService
{
    Task<PagedResultDto<SuppressionDto>> ListAsync(int? tenantId, string? search, int page, int pageSize, CancellationToken ct = default);
    Task<SuppressionResultDto> AddAsync(SuppressionCreateDto request, string actingUser, CancellationToken ct = default);
    Task<bool> RemoveAsync(int suppressionId, CancellationToken ct = default);
}

public interface ITenantAdminService
{
    Task<IReadOnlyList<TenantAdminDto>> ListAsync(CancellationToken ct = default);
    Task<TenantAdminDto?> UpdateSettingsAsync(int tenantId, TenantSettingsDto settings, CancellationToken ct = default);
}

// Leitura de ficheiros CSV/Excel para lotes (implementação na Infrastructure).
public interface IBatchFileParser
{
    ParsedBatchFile Parse(Stream content, string fileName);
}
