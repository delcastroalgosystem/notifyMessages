using System.Text.Json.Serialization;

namespace NotifyMessages.Infrastructure.Providers.Brevo.Models;

public class BrevoEmailRequest
{
    [JsonPropertyName("sender")]
    public BrevoEmailContact Sender { get; set; } = new();

    [JsonPropertyName("to")]
    public List<BrevoEmailContact> To { get; set; } = new();

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("htmlContent")]
    public string HtmlContent { get; set; } = string.Empty;

    [JsonPropertyName("textContent")]
    public string? TextContent { get; set; }

    [JsonPropertyName("replyTo")]
    public BrevoEmailContact? ReplyTo { get; set; }

    [JsonPropertyName("attachment")]
    public List<BrevoAttachment>? Attachment { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
}

public class BrevoEmailContact
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class BrevoAttachment
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class BrevoEmailResponse
{
    [JsonPropertyName("messageId")]
    public string? MessageId { get; set; }
}
