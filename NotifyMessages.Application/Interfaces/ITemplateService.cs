using NotifyMessages.Application.DTOs;

namespace NotifyMessages.Application.Interfaces;

public interface ITemplateService
{
    Task<IReadOnlyList<TemplateDto>> GetAllAsync(bool? isActive = null, CancellationToken ct = default);
    Task<TemplateDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<TemplateDto> CreateAsync(TemplateUpsertDto request, CancellationToken ct = default);
    Task<TemplateDto?> UpdateAsync(int id, TemplateUpsertDto request, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, CancellationToken ct = default);
    Task<TemplatePreviewResultDto?> PreviewAsync(int id, TemplatePreviewRequestDto request, CancellationToken ct = default);
}
