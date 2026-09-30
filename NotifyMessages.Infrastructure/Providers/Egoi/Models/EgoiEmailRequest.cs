using System.Text.Json.Serialization;

namespace NotifyMessages.Infrastructure.Providers.Egoi.Models;

public class EgoiEmailRequest
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    [JsonPropertyName("senderId")]
    public string SenderId { get; set; } = string.Empty;

    [JsonPropertyName("senderName")]
    public string SenderName { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public List<string> To { get; set; } = new();

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("htmlBody")]
    public string HtmlBody { get; set; } = string.Empty;

    [JsonPropertyName("openTracking")]
    public bool OpenTracking { get; set; }

    [JsonPropertyName("clickTracking")]
    public bool ClickTracking { get; set; }

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "non-urgent";

    [JsonPropertyName("registered")]
    public bool Registered { get; set; }

    [JsonPropertyName("attachedFiles")]
    public List<EgoiAttachment>? AttachedFiles { get; set; }

    // Devolvido tal como está em data.customData de cada evento do webhook: o Id do MessageDispatch.
    [JsonPropertyName("customData")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CustomData { get; set; }
}

public class EgoiAttachment
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = "application/octet-stream";
}
