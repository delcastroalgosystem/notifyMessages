using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotifyMessages.Domain.Entities;

// Gatilho de comunicação de um tenant: o quê (template), quando (agenda) e com que parâmetros.
// "Quem recebe" vem de fora: um conector (que corre o gatilho quando está devido), a API ou um ficheiro.
// Tabela MESSAGE_TRIGGER porque TRIGGER é palavra reservada do SQL Server.
[Table("MESSAGE_TRIGGER")]
public class MessageTrigger
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("TENANT_ID")]
    public int TenantId { get; set; }

    // Tipo que o conector entende (ex. "SMARTARENA.ANIVERSARIO") ou "FILE" para carga manual.
    [Required]
    [MaxLength(50)]
    [Column("CODE")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("NAME")]
    public string Name { get; set; } = string.Empty;

    [Column("TEMPLATE_ID")]
    public int TemplateId { get; set; }

    // Dia do mês (1-28) em que o gatilho corre. Nulo = todos os dias (ex. aniversário).
    [Column("SCHEDULE_DAY")]
    public byte? ScheduleDay { get; set; }

    // Hora local do tenant (TENANT.TIME_ZONE) a partir da qual o gatilho fica devido.
    [Column("SCHEDULE_TIME")]
    public TimeOnly ScheduleTime { get; set; }

    // Gatilho de intervalo: devido de N em N minutos (5 a 1440), ignorando dia e hora; cada execução com algo de
    // novo dá um lote. Nulo = gatilho diário/mensal (um lote por dia, SCHEDULE_DAY/SCHEDULE_TIME).
    [Column("INTERVAL_MINUTES")]
    public int? IntervalMinutes { get; set; }

    // Gatilho de intervalo: quando o conector entregou a última execução (UTC), mesmo sem nada de novo.
    [Column("LAST_RUN_AT")]
    public DateTime? LastRunAt { get; set; }

    // Nada é gerado antes desta data (controlo de volume ao ativar).
    [Column("START_DATE")]
    public DateOnly? StartDate { get; set; }

    // JSON com parâmetros do tipo de gatilho (ex. mínimo de quotas e antiguidade do aviso de cobrança).
    [Column("PARAMETERS", TypeName = "nvarchar(max)")]
    public string? Parameters { get; set; }

    // Os lotes deste gatilho ficam em PendingApproval até alguém os aprovar.
    [Column("REQUIRES_APPROVAL")]
    public bool RequiresApproval { get; set; }

    [Column("IS_ACTIVE")]
    public bool IsActive { get; set; }

    [MaxLength(256)]
    [Column("CREATED_BY")]
    public string? CreatedBy { get; set; }

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(256)]
    [Column("UPDATED_BY")]
    public string? UpdatedBy { get; set; }

    [Column("UPDATED_AT")]
    public DateTime? UpdatedAt { get; set; }
}
