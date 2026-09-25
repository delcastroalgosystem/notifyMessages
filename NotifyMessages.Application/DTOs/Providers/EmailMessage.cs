namespace NotifyMessages.Application.DTOs.Providers;

public class EmailMessage
{
    public string To { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    public string? ReplyTo { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new();
    public List<Attachment>? Attachments { get; set; }
    public bool OpenTracking { get; set; }
    public bool ClickTracking { get; set; }
    public string? ExternalTemplateId { get; set; }

    /// <summary>Id do MessageDispatch, usado para correlacionar eventos de webhook (tag/custom arg no provedor).</summary>
    public string? CorrelationId { get; set; }
}

public class Attachment
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
}
