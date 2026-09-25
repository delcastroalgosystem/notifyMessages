using System.Text.Json.Serialization;

namespace NotifyMessages.Infrastructure.Providers.Egoi.Models;

public class EgoiResponse
{
    [JsonPropertyName("messageId")]
    public string? Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
