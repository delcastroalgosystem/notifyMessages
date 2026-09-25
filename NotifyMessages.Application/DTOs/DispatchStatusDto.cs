using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.DTOs;

public class DispatchStatusDto
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public int TemplateId { get; set; }
    public string RecipientContact { get; set; } = string.Empty;
    public DispatchStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? ExternalId { get; set; }
    public string? ExternalKey { get; set; }
    public long? BatchId { get; set; }
    public DateTime? ScheduledAt { get; set; }
    // Contacto efetivamente usado (difere do RecipientContact em Sandbox).
    public string? SentTo { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public IReadOnlyList<DispatchEventDto> Events { get; set; } = Array.Empty<DispatchEventDto>();
}

public class DispatchEventDto
{
    public string EventType { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
}
