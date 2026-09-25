using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Domain.Entities;

[Table("TENANT_PROVIDER_CONFIG")]
public class TenantProviderConfig
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Required]
    [Column("TENANT_ID")]
    public int TenantId { get; set; }

    [Required]
    [Column("PROVIDER_TYPE")]
    public ProviderType ProviderType { get; set; }

    [Column("IS_GLOBAL")]
    public bool IsGlobal { get; set; }

    /// <summary>
    /// Legado: segredo em texto simples na BD. Novos registos devem usar <see cref="SecretName"/>.
    /// </summary>
    [Column("API_KEY")]
    public string? ApiKey { get; set; }

    /// <summary>
    /// Nome dos segredos na configuração (ProviderSettings:Tenants:{SecretName}:ApiKey / :AuthToken).
    /// Quando preenchido, ApiKey e AuthToken vêm da configuração e as colunas da BD são ignoradas.
    /// </summary>
    [MaxLength(100)]
    [Column("SECRET_NAME")]
    public string? SecretName { get; set; }

    [MaxLength(200)]
    [Column("BASE_URL")]
    public string? BaseUrl { get; set; }

    [MaxLength(100)]
    [Column("DOMAIN")]
    public string? Domain { get; set; }

    [MaxLength(100)]
    [Column("SENDER_ID")]
    public string? SenderId { get; set; }

    [MaxLength(100)]
    [Column("SENDER_NAME")]
    public string? SenderName { get; set; }

    [MaxLength(100)]
    [Column("ACCOUNT_SID")]
    public string? AccountSid { get; set; }

    [Column("AUTH_TOKEN")]
    public string? AuthToken { get; set; }

    [MaxLength(20)]
    [Column("FROM_NUMBER")]
    public string? FromNumber { get; set; }

    [Column("LIST_ID")]
    public int? ListId { get; set; }

    [Column("IS_ACTIVE")]
    public bool IsActive { get; set; } = true;

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("UPDATED_AT")]
    public DateTime? UpdatedAt { get; set; }
}
