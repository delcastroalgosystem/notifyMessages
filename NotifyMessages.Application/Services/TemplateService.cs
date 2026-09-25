using Microsoft.EntityFrameworkCore;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Domain.Entities;

namespace NotifyMessages.Application.Services;

public class TemplateService : ITemplateService
{
    private readonly IAppDbContext _context;

    public TemplateService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TemplateDto>> GetAllAsync(int? tenantId, bool? isActive = null, CancellationToken ct = default)
    {
        var query = Visible(tenantId).AsNoTracking();
        if (isActive.HasValue)
        {
            query = query.Where(t => t.IsActive == isActive.Value);
        }

        var templates = await query.OrderBy(t => t.Name).ToListAsync(ct);
        return templates.Select(ToDto).ToList();
    }

    public async Task<TemplateDto?> GetByIdAsync(int? tenantId, int id, CancellationToken ct = default)
    {
        var template = await Visible(tenantId).AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        return template is null ? null : ToDto(template);
    }

    public async Task<TemplateDto> CreateAsync(int? tenantId, TemplateUpsertDto request, CancellationToken ct = default)
    {
        var template = new Template { TenantId = tenantId };
        Apply(template, request);

        _context.Templates.Add(template);
        await _context.SaveChangesAsync(ct);

        return ToDto(template);
    }

    public async Task<TemplateDto?> UpdateAsync(int? tenantId, int id, TemplateUpsertDto request, CancellationToken ct = default)
    {
        var template = await Editable(tenantId).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
        {
            return null;
        }

        Apply(template, request);
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return ToDto(template);
    }

    public async Task<bool> DeactivateAsync(int? tenantId, int id, CancellationToken ct = default)
    {
        var template = await Editable(tenantId).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
        {
            return false;
        }

        template.IsActive = false;
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TemplatePreviewResultDto?> PreviewAsync(int? tenantId, int id, TemplatePreviewRequestDto request, CancellationToken ct = default)
    {
        var template = await Visible(tenantId).AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
        {
            return null;
        }

        var variables = request.Variables ?? new Dictionary<string, string>();
        var requiredVariables = TemplateVariableRenderer.ExtractVariableNames(template.Subject, template.HtmlBody, template.TextBody);
        var missingVariables = requiredVariables
            .Where(name => !variables.Keys.Any(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return new TemplatePreviewResultDto
        {
            Subject = TemplateVariableRenderer.Render(template.Subject, variables),
            HtmlBody = TemplateVariableRenderer.Render(template.HtmlBody, variables),
            TextBody = TemplateVariableRenderer.Render(template.TextBody, variables),
            MissingVariables = missingVariables
        };
    }

    // Os do tenant e os partilhados; administração (null) vê todos.
    private IQueryable<Template> Visible(int? tenantId)
        => tenantId is null ? _context.Templates : _context.Templates.Where(t => t.TenantId == tenantId || t.TenantId == null);

    // Um tenant só altera os seus; os partilhados só pela administração (null).
    private IQueryable<Template> Editable(int? tenantId)
        => tenantId is null ? _context.Templates : _context.Templates.Where(t => t.TenantId == tenantId);

    private static void Apply(Template template, TemplateUpsertDto request)
    {
        template.Name = request.Name;
        template.Channel = request.Channel;
        template.ProviderType = request.ProviderType;
        template.UseCampaignMode = request.UseCampaignMode;
        template.Subject = request.Subject;
        template.HtmlBody = request.HtmlBody;
        template.TextBody = request.TextBody;
        template.SenderId = request.SenderId;
        template.SenderName = request.SenderName;
        template.ExternalTemplateId = request.ExternalTemplateId;
        template.ListId = request.ListId;
        template.IsActive = request.IsActive;
    }

    private static TemplateDto ToDto(Template template) => new()
    {
        Id = template.Id,
        TenantId = template.TenantId,
        Name = template.Name,
        Channel = template.Channel,
        ProviderType = template.ProviderType,
        UseCampaignMode = template.UseCampaignMode,
        Subject = template.Subject,
        HtmlBody = template.HtmlBody,
        TextBody = template.TextBody,
        SenderId = template.SenderId,
        SenderName = template.SenderName,
        ExternalTemplateId = template.ExternalTemplateId,
        ListId = template.ListId,
        IsActive = template.IsActive,
        CreatedAt = template.CreatedAt,
        UpdatedAt = template.UpdatedAt,
        Variables = TemplateVariableRenderer.ExtractVariableNames(template.Subject, template.HtmlBody, template.TextBody)
    };
}
