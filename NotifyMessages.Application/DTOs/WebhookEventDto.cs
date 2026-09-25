using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.DTOs;

public class WebhookEventDto
{
    /// <summary>Id do MessageDispatch, quando o provedor devolveu a nossa tag/custom data.</summary>
    public long? DispatchId { get; set; }

    /// <summary>Id de mensagem do próprio provedor, usado como correlação alternativa (MessageDispatch.ExternalId).</summary>
    public string? ExternalId { get; set; }

    /// <summary>Nome do evento tal como reportado pelo provedor (ex: "delivered", "hard_bounce"), guardado para auditoria.</summary>
    public required string EventType { get; set; }

    /// <summary>Status normalizado a aplicar ao MessageDispatch, se o evento representar progresso de entrega. Null = só regista o evento, sem mudar o status.</summary>
    public DispatchStatus? MappedStatus { get; set; }

    public DateTime EventDate { get; set; }

    public string RawPayload { get; set; } = string.Empty;
}
