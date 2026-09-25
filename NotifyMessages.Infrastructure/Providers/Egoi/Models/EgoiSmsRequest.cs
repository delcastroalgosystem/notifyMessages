using System.Text.Json.Serialization;

namespace NotifyMessages.Infrastructure.Providers.Egoi.Models;

public class EgoiSmsRequest
{
    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("textBody")]
    public string TextBody { get; set; } = string.Empty;

    [JsonPropertyName("encoding")]
    public string Encoding { get; set; } = "utf-8";

    [JsonPropertyName("maxCount")]
    public int MaxCount { get; set; } = 5;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "normal";

    [JsonPropertyName("registered")]
    public bool Registered { get; set; }

    [JsonPropertyName("group")]
    public string Group { get; set; } = "default";

    [JsonPropertyName("scheduleTo")]
    public string? ScheduleTo { get; set; }
}
