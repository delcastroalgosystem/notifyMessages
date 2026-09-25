using NotifyMessages.Application.DTOs;

namespace NotifyMessages.Application.Interfaces;

// tenantId = tenant autenticado: vê os seus templates e os partilhados (TENANT_ID nulo), só altera os seus
// e os que cria ficam seus. tenantId = null = administração da plataforma: vê e altera todos, cria partilhados.
public interface ITemplateService
{
    Task<IReadOnlyList<TemplateDto>> GetAllAsync(int? tenantId, bool? isActive = null, CancellationToken ct = default);
    Task<TemplateDto?> GetByIdAsync(int? tenantId, int id, CancellationToken ct = default);
    Task<TemplateDto> CreateAsync(int? tenantId, TemplateUpsertDto request, CancellationToken ct = default);
    Task<TemplateDto?> UpdateAsync(int? tenantId, int id, TemplateUpsertDto request, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int? tenantId, int id, CancellationToken ct = default);
    Task<TemplatePreviewResultDto?> PreviewAsync(int? tenantId, int id, TemplatePreviewRequestDto request, CancellationToken ct = default);
}
