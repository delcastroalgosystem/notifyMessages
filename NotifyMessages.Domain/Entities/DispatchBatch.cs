using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Domain.Entities;

// Lote de envios: o resultado de uma execução de gatilho (conector), de uma chamada à API ou de um ficheiro.
// Os itens são linhas de MESSAGE_DISPATCH com BATCH_ID.
[Table("DISPATCH_BATCH")]
public class DispatchBatch
{
    [Key]
    [Column("ID")]
    public long Id { get; set; }

    [Column("TENANT_ID")]
    public int TenantId { get; set; }

    [Column("TRIGGER_ID")]
    public int? TriggerId { get; set; }

    [Column("SOURCE")]
    public BatchSource Source { get; set; }

    // Dia em que o gatilho correu (hora local do tenant). Um lote de conector por gatilho e por dia.
    [Column("RUN_DATE")]
    public DateOnly? RunDate { get; set; }

    // Data a que o conteúdo se refere (ex. mês de referência de um aviso de quota).
    [Column("REFERENCE_DATE")]
    public DateOnly? ReferenceDate { get; set; }

    [Column("STATUS")]
    public BatchStatus Status { get; set; }

    [Column("RECEIVED")]
    public int Received { get; set; }

    [Column("ACCEPTED")]
    public int Accepted { get; set; }

    [Column("DUPLICATES")]
    public int Duplicates { get; set; }

    [Column("SUPPRESSED")]
    public int Suppressed { get; set; }

    [Column("REJECTED")]
    public int Rejected { get; set; }

    // JSON: excluídos pela origem (ex. sócios sem quota), linhas inválidas de um ficheiro, avisos.
    [Column("DETAILS", TypeName = "nvarchar(max)")]
    public string? Details { get; set; }

    [MaxLength(260)]
    [Column("FILE_NAME")]
    public string? FileName { get; set; }

    [MaxLength(256)]
    [Column("CREATED_BY")]
    public string? CreatedBy { get; set; }

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(256)]
    [Column("APPROVED_BY")]
    public string? ApprovedBy { get; set; }

    [Column("APPROVED_AT")]
    public DateTime? ApprovedAt { get; set; }

    [Column("COMPLETED_AT")]
    public DateTime? CompletedAt { get; set; }
}
