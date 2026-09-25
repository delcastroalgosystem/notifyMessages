using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Domain.Entities;

// Contacto que não deve receber nada deste tenant (opt-out). Aplicada ao aceitar os itens de um lote.
[Table("SUPPRESSION")]
public class Suppression
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("TENANT_ID")]
    public int TenantId { get; set; }

    // Guardado normalizado (sem espaços, minúsculas).
    [Required]
    [MaxLength(100)]
    [Column("CONTACT")]
    public string Contact { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("REASON")]
    public string? Reason { get; set; }

    [Column("SOURCE")]
    public SuppressionSource Source { get; set; } = SuppressionSource.Manual;

    [MaxLength(256)]
    [Column("CREATED_BY")]
    public string? CreatedBy { get; set; }

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
