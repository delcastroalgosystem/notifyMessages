using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotifyMessages.Domain.Entities;

[Table("TENANT")]
public class Tenant
{
    [Column("ID")]
    public int Id { get; set; }

    // Nome real da entidade (ex. "Associação Académica de Coimbra").
    [Column("NAME")]
    public string Name { get; set; } = string.Empty;

    // Referência de ligação ao software externo do cliente (ERP/CRM), ex. "CLUBE_AAC". Única quando preenchida.
    [MaxLength(50)]
    [Column("REF_NAME")]
    public string? RefName { get; set; }

    [Column("DOCUMENT_ID")]
    public string? DocumentId { get; set; }

    [Column("ACTIVE")]
    public bool Active { get; set; } = true;

    [Column("API_KEY_HASH")]
    public string? ApiKeyHash { get; set; }

    // Interruptor geral: desligado, o Worker não envia nada deste tenant (as mensagens ficam em fila).
    [Column("SENDING_ENABLED")]
    public bool SendingEnabled { get; set; } = true;

    // Endereço de teste: preenchido, todos os e-mails do tenant vão para aqui (Sandbox).
    [MaxLength(100)]
    [Column("SANDBOX_CONTACT")]
    public string? SandboxContact { get; set; }

    // Fuso horário IANA das agendas dos gatilhos (ex. "Europe/Lisbon", "America/Sao_Paulo").
    [Required]
    [MaxLength(64)]
    [Column("TIME_ZONE")]
    public string TimeZone { get; set; } = "Europe/Lisbon";
}
