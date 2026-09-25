using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.DTOs.Providers;

public class ProviderConfig
{
    public ProviderType ProviderType { get; set; }
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string? Domain { get; set; }
    public string? SenderId { get; set; }
    public string? SenderName { get; set; }
    public string? AccountSid { get; set; }
    public string? AuthToken { get; set; }
    public string? FromNumber { get; set; }
    public int? ListId { get; set; }
    public Dictionary<string, string> ExtraSettings { get; set; } = new();
}
