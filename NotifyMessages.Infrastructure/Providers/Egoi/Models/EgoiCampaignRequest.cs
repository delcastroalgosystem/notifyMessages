using System.Text.Json.Serialization;

namespace NotifyMessages.Infrastructure.Providers.Egoi.Models;

public class EgoiCampaignRequest
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("senderName")]
    public string SenderName { get; set; } = string.Empty;

    [JsonPropertyName("senderEmail")]
    public string SenderEmail { get; set; } = string.Empty;

    [JsonPropertyName("listId")]
    public int ListId { get; set; }

    [JsonPropertyName("htmlBody")]
    public string HtmlBody { get; set; } = string.Empty;

    [JsonPropertyName("openTracking")]
    public bool OpenTracking { get; set; }

    [JsonPropertyName("clickTracking")]
    public bool ClickTracking { get; set; }
}

public class EgoiCampaignResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public class EgoiSendResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}
