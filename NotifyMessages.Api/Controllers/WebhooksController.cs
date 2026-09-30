using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotifyMessages.Api.Webhooks;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Api.Controllers;

/// <summary>
/// Endpoints chamados diretamente pelos provedores de mensagens (não por tenants) para reportar
/// eventos de entrega (Delivered, Bounced, Read). Autenticados por token partilhado na query string
/// (WebhookSettings:SharedSecret), não pela API Key de tenant.
/// </summary>
[ApiController]
[Route("api/v1/webhooks")]
[AllowAnonymous]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookEventService _webhookEventService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(IWebhookEventService webhookEventService, IConfiguration configuration, ILogger<WebhooksController> logger)
    {
        _webhookEventService = webhookEventService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("sendgrid")]
    public async Task<IActionResult> SendGrid([FromQuery] string? token, CancellationToken ct)
    {
        if (!IsTokenValid(token))
        {
            return Unauthorized();
        }

        var rawBody = await ReadBodyAsync(ct);
        List<JsonElement> events;
        try
        {
            events = JsonSerializer.Deserialize<List<JsonElement>>(rawBody) ?? new();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Payload de webhook SendGrid inválido. Raw: {Raw}", rawBody);
            return Ok();
        }

        foreach (var evt in events)
        {
            var eventType = GetString(evt, "event") ?? "unknown";
            var dispatchIdStr = GetString(evt, "dispatch_id");
            var externalId = GetString(evt, "sg_message_id");
            var timestamp = evt.TryGetProperty("timestamp", out var ts) && ts.TryGetInt64(out var tsValue)
                ? tsValue
                : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            await _webhookEventService.ProcessEventAsync(new WebhookEventDto
            {
                DispatchId = long.TryParse(dispatchIdStr, out var dispatchId) ? dispatchId : null,
                ExternalId = externalId,
                EventType = eventType,
                MappedStatus = MapSendGridEvent(eventType),
                EventDate = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime,
                RawPayload = evt.GetRawText()
            }, ct);
        }

        return Ok();
    }

    private static DispatchStatus? MapSendGridEvent(string eventType) => eventType switch
    {
        "delivered" => DispatchStatus.Delivered,
        "open" => DispatchStatus.Read,
        "bounce" or "dropped" or "spamreport" => DispatchStatus.Bounced,
        _ => null
    };

    [HttpPost("brevo")]
    public async Task<IActionResult> Brevo([FromQuery] string? token, CancellationToken ct)
    {
        if (!IsTokenValid(token))
        {
            return Unauthorized();
        }

        var rawBody = await ReadBodyAsync(ct);
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawBody);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Payload de webhook Brevo inválido. Raw: {Raw}", rawBody);
            return Ok();
        }

        using (doc)
        {
            var root = doc.RootElement;
            var eventType = GetString(root, "event") ?? "unknown";
            var externalId = GetString(root, "message-id");

            string? dispatchIdStr = null;
            if (root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array && tagsEl.GetArrayLength() > 0)
            {
                dispatchIdStr = tagsEl[0].GetString();
            }
            dispatchIdStr ??= GetString(root, "tag");

            var eventDate = GetString(root, "date") is { } dateStr && DateTime.TryParse(dateStr, out var parsedDate)
                ? DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc)
                : DateTime.UtcNow;

            await _webhookEventService.ProcessEventAsync(new WebhookEventDto
            {
                DispatchId = long.TryParse(dispatchIdStr, out var dispatchId) ? dispatchId : null,
                ExternalId = externalId,
                EventType = eventType,
                MappedStatus = MapBrevoEvent(eventType),
                EventDate = eventDate,
                RawPayload = rawBody
            }, ct);
        }

        return Ok();
    }

    private static DispatchStatus? MapBrevoEvent(string eventType) => eventType switch
    {
        "delivered" => DispatchStatus.Delivered,
        "opened" or "unique_opened" or "click" => DispatchStatus.Read,
        "hard_bounce" or "soft_bounce" or "blocked" or "spam" or "invalid_email" or "error" => DispatchStatus.Bounced,
        _ => null
    };

    [HttpPost("twilio")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Twilio([FromQuery] string? token, [FromQuery] long? dispatchId, CancellationToken ct)
    {
        if (!IsTokenValid(token))
        {
            return Unauthorized();
        }

        var form = await Request.ReadFormAsync(ct);
        var messageStatus = form["MessageStatus"].ToString();
        var messageSid = form["MessageSid"].ToString();
        var rawPayload = JsonSerializer.Serialize(form.ToDictionary(f => f.Key, f => f.Value.ToString()));

        await _webhookEventService.ProcessEventAsync(new WebhookEventDto
        {
            DispatchId = dispatchId,
            ExternalId = string.IsNullOrEmpty(messageSid) ? null : messageSid,
            EventType = string.IsNullOrEmpty(messageStatus) ? "unknown" : messageStatus,
            MappedStatus = MapTwilioStatus(messageStatus),
            EventDate = DateTime.UtcNow,
            RawPayload = rawPayload
        }, ct);

        return Ok();
    }

    private static DispatchStatus? MapTwilioStatus(string status) => status switch
    {
        "delivered" => DispatchStatus.Delivered,
        "read" => DispatchStatus.Read,
        "failed" or "undelivered" => DispatchStatus.Bounced,
        _ => null
    };

    /// <summary>
    /// Webhook transacional da E-goi (Slingshot v2), registado por <c>Scripts\Register-EgoiWebhook.ps1</c>.
    /// Formato conforme a documentação (secção "Webhooks"): ver <see cref="EgoiWebhookParser"/>.
    /// Ainda não validado com um callback real; o payload de cada evento fica em MESSAGE_DISPATCH_EVENTS.
    /// </summary>
    [HttpPost("egoi")]
    public async Task<IActionResult> Egoi([FromQuery] string? token, CancellationToken ct)
    {
        if (!IsTokenValid(token))
        {
            return Unauthorized();
        }

        var rawBody = await ReadBodyAsync(ct);
        List<WebhookEventDto> eventos;
        try
        {
            eventos = EgoiWebhookParser.Parse(rawBody);
        }
        catch (JsonException ex)
        {
            // Sem o corpo no log: traz o e-mail do destinatário.
            _logger.LogWarning(ex, "Payload de webhook E-goi não pôde ser interpretado como JSON ({Tamanho} caracteres).", rawBody.Length);
            return Ok();
        }

        foreach (var evento in eventos)
        {
            await _webhookEventService.ProcessEventAsync(evento, ct);
        }

        return Ok();
    }

    private static string? GetString(JsonElement el, string propertyName)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private async Task<string> ReadBodyAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        return await reader.ReadToEndAsync(ct);
    }

    private bool IsTokenValid(string? token)
    {
        var expected = _configuration["WebhookSettings:SharedSecret"];
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(token))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(token);
        return expectedBytes.Length == actualBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
