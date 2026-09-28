using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.DTOs;

// DTOs da API de administração (/api/v1/admin/...), usada pela UI do Smartarena ExtraTools e, no futuro,
// pelo portal do NotifyMessages. Todas as operações indicam o tenant explicitamente.

public class PagedResultDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class TenantAdminDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? RefName { get; set; }
    public bool Active { get; set; }
    public bool SendingEnabled { get; set; }
    public string? SandboxContact { get; set; }
    public string TimeZone { get; set; } = string.Empty;
}

public class TenantSettingsDto
{
    public bool SendingEnabled { get; set; }
    public string? SandboxContact { get; set; }
    public string TimeZone { get; set; } = "Europe/Lisbon";
}

public class AdminTemplateUpsertDto : TemplateUpsertDto
{
    // Dono do template na criação (nulo = partilhado). Ignorado na atualização.
    public int? TenantId { get; set; }
}

public class TriggerDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int TemplateId { get; set; }
    public byte? ScheduleDay { get; set; }
    public TimeOnly ScheduleTime { get; set; }
    public int? IntervalMinutes { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateOnly? StartDate { get; set; }
    public string? Parameters { get; set; }
    public bool RequiresApproval { get; set; }
    public bool IsActive { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class TriggerUpsertDto
{
    // Só na criação; um gatilho não muda de tenant.
    public int TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int TemplateId { get; set; }
    public byte? ScheduleDay { get; set; }
    public TimeOnly ScheduleTime { get; set; }
    // De N em N minutos (5 a 1440); nulo = diário/mensal pela agenda
    public int? IntervalMinutes { get; set; }
    public DateOnly? StartDate { get; set; }
    public string? Parameters { get; set; }
    public bool RequiresApproval { get; set; } = true;
    public bool IsActive { get; set; }
}

public class BatchQueryDto
{
    public int? TenantId { get; set; }
    public int? TriggerId { get; set; }
    public BatchStatus? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class DispatchQueryDto
{
    public int? TenantId { get; set; }
    public int? TriggerId { get; set; }
    public long? BatchId { get; set; }
    public DispatchStatus? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    // Procura em contacto, nome ou chave externa.
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class DispatchListItemDto
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public int TemplateId { get; set; }
    public int? TriggerId { get; set; }
    public long? BatchId { get; set; }
    public string? ExternalKey { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientContact { get; set; } = string.Empty;
    public string? SentTo { get; set; }
    public DispatchStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public class DispatchHistoryDto : PagedResultDto<DispatchListItemDto>
{
    // Contagem por estado de todos os envios que cumprem o filtro (não só da página).
    public Dictionary<string, int> CountsByStatus { get; set; } = [];
}

public class SuppressionDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Contact { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public SuppressionSource Source { get; set; }
    public string SourceName => Source.ToString();
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SuppressionCreateDto
{
    public int TenantId { get; set; }
    public string Contact { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public SuppressionSource Source { get; set; } = SuppressionSource.Manual;
}

public class SuppressionResultDto
{
    public SuppressionDto Suppression { get; set; } = new();
    public bool AlreadyExisted { get; set; }
    // Envios ainda por enviar (Held/Queued) deste contacto que passaram a Suppressed.
    public int PendingDispatchesSuppressed { get; set; }
}

// Resultado da leitura de um ficheiro CSV/Excel antes de criar o lote.
public class ParsedBatchFile
{
    public List<BatchItemDto> Items { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}
