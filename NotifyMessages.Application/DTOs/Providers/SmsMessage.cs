namespace NotifyMessages.Application.DTOs.Providers;

public class SmsMessage
{
    public string To { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string TextBody { get; set; } = string.Empty;
    public Dictionary<string, string> Variables { get; set; } = new();
    public int MaxParts { get; set; } = 5;
    public string? ScheduleTo { get; set; }

    /// <summary>Id do MessageDispatch, usado para correlacionar eventos de webhook (ex: StatusCallback do Twilio).</summary>
    public string? CorrelationId { get; set; }
}
