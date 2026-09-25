using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.DTOs;

// POST /api/v1/batches. Com TriggerId + RunDate é um lote de conector (um por gatilho e por dia);
// sem TriggerId é um lote de API, e o TemplateId é obrigatório.
public class BatchCreateDto
{
    public int? TriggerId { get; set; }
    public int? TemplateId { get; set; }
    public DateOnly? RunDate { get; set; }
    public DateOnly? ReferenceDate { get; set; }
    public List<BatchItemDto> Items { get; set; } = [];
    // Candidatos que a origem deixou de fora e porquê (ex. "sem quota para o mês de referência").
    public List<BatchExcludedDto> Excluded { get; set; } = [];
    // Avisos gerais da origem (ex. "quotas da época ainda não geradas").
    public List<string> Warnings { get; set; } = [];
}

public class BatchItemDto
{
    // Obrigatória: é a chave de idempotência (única por tenant), ex. "SOCIO:1234:2026".
    public string ExternalKey { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientContact { get; set; } = string.Empty;
    public Dictionary<string, string>? BusinessData { get; set; }
    public DateTime? ScheduledAt { get; set; }
}

public class BatchExcludedDto
{
    public string Reference { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class BatchDto
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public int? TriggerId { get; set; }
    public BatchSource Source { get; set; }
    public string SourceName => Source.ToString();
    public DateOnly? RunDate { get; set; }
    public DateOnly? ReferenceDate { get; set; }
    public BatchStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int Received { get; set; }
    public int Accepted { get; set; }
    public int Duplicates { get; set; }
    public int Suppressed { get; set; }
    public int Rejected { get; set; }
    // Estado atual dos envios do lote, por estado (ex. { "Held": 10 } ou { "Sent": 9, "Failed": 1 }).
    public Dictionary<string, int> Progress { get; set; } = [];
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
