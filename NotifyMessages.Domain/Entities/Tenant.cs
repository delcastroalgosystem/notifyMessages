using System.ComponentModel.DataAnnotations.Schema;

namespace NotifyMessages.Domain.Entities;

[Table("TENANT")]
public class Tenant
{
    [Column("ID")]
    public int Id { get; set; }
    
    [Column("NAME")]
    public string Name { get; set; } = string.Empty;
    
    [Column("DOCUMENT_ID")]
    public string? DocumentId { get; set; }

    [Column("ACTIVE")]
    public bool Active { get; set; } = true;

    [Column("API_KEY_HASH")]
    public string? ApiKeyHash { get; set; }
}
