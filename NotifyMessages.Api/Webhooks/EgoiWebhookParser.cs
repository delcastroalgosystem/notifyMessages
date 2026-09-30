using System.Text.Json;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Api.Webhooks;

/// <summary>
/// Eventos do webhook transacional da E-goi (Slingshot v2, "Webhooks"): uma lista de
/// <c>{ "action": "SENT", "actionDate": &lt;ms&gt;, "data": { "messageId": 1693, "customData": "..." } }</c>.
/// <c>customData</c> é o Id do MessageDispatch enviado com a mensagem (EgoiEmailProvider); <c>messageId</c>
/// fica como correlação alternativa com MessageDispatch.ExternalId.
/// </summary>
public static class EgoiWebhookParser
{
    /// <exception cref="JsonException">O corpo não é JSON.</exception>
    public static List<WebhookEventDto> Parse(string rawBody)
    {
        using var doc = JsonDocument.Parse(rawBody);
        var elements = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToList()
            : [doc.RootElement];

        var eventos = new List<WebhookEventDto>();
        foreach (var evt in elements.Where(e => e.ValueKind == JsonValueKind.Object))
        {
            // Os campos da mensagem vêm em "data"; aceita-os também no nível de cima.
            var data = evt.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : evt;

            var action = (GetText(evt, "action") ?? GetText(evt, "event") ?? "unknown").Trim().ToLowerInvariant();
            var customData = GetText(data, "customData") ?? GetText(data, "custom_data")
                ?? GetText(evt, "customData") ?? GetText(evt, "custom_data");
            var messageId = GetText(data, "messageId") ?? GetText(data, "message_id")
                ?? GetText(evt, "messageId") ?? GetText(evt, "message_id") ?? GetText(evt, "messageHash");

            eventos.Add(new WebhookEventDto
            {
                DispatchId = long.TryParse(customData?.Trim(), out var dispatchId) ? dispatchId : null,
                ExternalId = messageId,
                EventType = action,
                MappedStatus = MapAction(action),
                EventDate = GetActionDate(evt) ?? DateTime.UtcNow,
                RawPayload = evt.GetRawText()
            });
        }
        return eventos;
    }

    // Ações da E-goi (Webhook_Email.actions); os nomes antigos ficam por compatibilidade.
    public static DispatchStatus? MapAction(string action) => action.ToLowerInvariant() switch
    {
        "sent" => DispatchStatus.Delivered,          // aceite pelo servidor de destino
        "view" or "open" or "click" => DispatchStatus.Read,
        "bounce" or "soft_bounce" or "hard_bounce" => DispatchStatus.Bounced,
        "abuse" or "spam_complaint" => DispatchStatus.Bounced,
        "failed" or "canceled" => DispatchStatus.Failed,
        _ => null                                      // processed, remove...: só fica registado
    };

    private static DateTime? GetActionDate(JsonElement evt)
    {
        if (evt.TryGetProperty("actionDate", out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var ms))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        }
        return null;
    }

    // Texto ou número (a E-goi manda o messageId como número nos eventos e como texto no envio).
    private static string? GetText(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.GetRawText(),
            _ => null
        };
    }
}
