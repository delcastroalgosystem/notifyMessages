using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Domain.Entities;

[Table("MESSAGE_DISPATCH")]
public class MessageDispatch
{
	[Key]
	[Column("ID")]
	public long Id { get; set; }

	[Required]
	[Column("TENANT_ID")]
	public int TenantId { get; set; }

	[Required]
	[Column("TEMPLATE_ID")]
	public int TemplateId { get; set; }

	[Required]
	[MaxLength(100)]
	[Column("IDEMPOTENCY_KEY")]
	public required string IdempotencyKey { get; set; }

	[MaxLength(100)]
	[Column("EXTERNAL_ID")]
	public string? ExternalId { get; set; }

	[Required]
	[MaxLength(100)]
	[Column("RECIPIENT_NAME")]
	public required string RecipientName { get; set; }

	[Required]
	[MaxLength(100)]
	[Column("RECIPIENT_CONTACT")]
	public required string RecipientContact { get; set; }

	[Column("CONTEXT_DATA_JSON", TypeName = "nvarchar(max)")]
	public string ContextDataJson { get; private set; } = "{}";

	[Required]
	[Column("CURRENT_STATUS")]
	public DispatchStatus CurrentStatus { get; set; } = DispatchStatus.Queued;

	[Column("CREATED_AT")]
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	[Column("PROCESSED_AT")]
	public DateTime? ProcessedAt { get; set; }

	[Column("RETRY_COUNT")]
	public int RetryCount { get; set; } = 0;

	[Column("ERROR_LOG")]
	public string? ErrorLog { get; set; }

	// Métodos Auxiliares
	public void SetContextData(object data)
	{
		if (data == null) return;
		this.ContextDataJson = JsonSerializer.Serialize(data);
	}

	public T? GetContextData<T>()
	{
		if (string.IsNullOrEmpty(ContextDataJson)) return default;
		return JsonSerializer.Deserialize<T>(ContextDataJson);
	}
}
