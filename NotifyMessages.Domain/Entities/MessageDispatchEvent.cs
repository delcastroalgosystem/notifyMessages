using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotifyMessages.Domain.Entities;

[Table("MESSAGE_DISPATCH_EVENTS")]
public class MessageDispatchEvent
{
    [Key]
    [Column("ID")]
    public long Id { get; set; }

    [Required]
    [Column("DISPATCH_ID")]
    public long DispatchId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("EVENT_TYPE")]
    public string EventType { get; set; } = string.Empty;

    [Column("EVENT_DATE")]
    public DateTime EventDate { get; set; } = DateTime.UtcNow;

    [Column("PROVIDER_RESPONSE", TypeName = "nvarchar(max)")]
    public string? ProviderResponse { get; set; }

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
