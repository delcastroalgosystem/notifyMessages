using System.Text.Json.Serialization;

namespace NotifyMessages.Infrastructure.Providers.Brevo.Models;

public class BrevoSmsRequest
{
    [JsonPropertyName("sender")]
    public string Sender { get; set; } = string.Empty;

    [JsonPropertyName("recipient")]
    public string Recipient { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "transactional";
}

public class BrevoSmsResponse
{
    [JsonPropertyName("messageId")]
    public long? MessageId { get; set; }
}
