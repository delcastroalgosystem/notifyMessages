namespace NotifyMessages.Application.DTOs.Providers;

public class CampaignRequest
{
    public string CampaignName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public int ListId { get; set; }
    public List<CampaignContact> Contacts { get; set; } = new();
    public Dictionary<string, string> Variables { get; set; } = new();
    public bool OpenTracking { get; set; }
    public bool ClickTracking { get; set; }
}

public class CampaignContact
{
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public Dictionary<string, string> CustomFields { get; set; } = new();
}
