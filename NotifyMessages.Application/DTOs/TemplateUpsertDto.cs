using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.DTOs;

public class TemplateUpsertDto
{
    public string Name { get; set; } = string.Empty;
    public ChannelType Channel { get; set; }
    public ProviderType ProviderType { get; set; }
    public bool UseCampaignMode { get; set; }
    public string? Subject { get; set; }
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
    public string? SenderId { get; set; }
    public string? SenderName { get; set; }
    public int? ExternalTemplateId { get; set; }
    public int? ListId { get; set; }
    public bool IsActive { get; set; } = true;
}
