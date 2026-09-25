using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Domain.Entities;

[Table("TEMPLATE")]
public class Template
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("NAME")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("CHANNEL")]
    public ChannelType Channel { get; set; }

    [Required]
    [Column("PROVIDER_TYPE")]
    public ProviderType ProviderType { get; set; }

    [Column("USE_CAMPAIGN_MODE")]
    public bool UseCampaignMode { get; set; }

    [MaxLength(200)]
    [Column("SUBJECT")]
    public string? Subject { get; set; }

    [Column("HTML_BODY")]
    public string? HtmlBody { get; set; }

    [Column("TEXT_BODY")]
    public string? TextBody { get; set; }

    [MaxLength(100)]
    [Column("SENDER_ID")]
    public string? SenderId { get; set; }

    [MaxLength(100)]
    [Column("SENDER_NAME")]
    public string? SenderName { get; set; }

    [Column("EXTERNAL_TEMPLATE_ID")]
    public int? ExternalTemplateId { get; set; }

    [Column("LIST_ID")]
    public int? ListId { get; set; }

    [Column("IS_ACTIVE")]
    public bool IsActive { get; set; } = true;

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("UPDATED_AT")]
    public DateTime? UpdatedAt { get; set; }
}
