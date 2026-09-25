using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Domain.Enums;
using NotifyMessages.Application.Interfaces;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace NotifyMessages.Infrastructure.Providers.Twilio;

public class TwilioSmsProvider : ISmsProvider
{
    private readonly ILogger<TwilioSmsProvider> _logger;
    private readonly IConfiguration _configuration;

    public ProviderType Type => ProviderType.Twilio;

    public TwilioSmsProvider(ILogger<TwilioSmsProvider> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<ProviderResult> SendAsync(SmsMessage message, ProviderConfig config, CancellationToken ct = default)
    {
        try
        {
            TwilioClient.Init(config.AccountSid, config.AuthToken);

            var messageOptions = new CreateMessageOptions(new PhoneNumber(message.To))
            {
                From = new PhoneNumber(message.From),
                Body = message.TextBody
            };

            var statusCallbackUrl = BuildStatusCallbackUrl(message.CorrelationId);
            if (statusCallbackUrl != null)
            {
                messageOptions.StatusCallback = statusCallbackUrl;
            }

            var twilioMessage = await MessageResource.CreateAsync(messageOptions);

            if (twilioMessage.Status == MessageResource.StatusEnum.Queued ||
                twilioMessage.Status == MessageResource.StatusEnum.Sent)
            {
                _logger.LogInformation("SMS enviado com sucesso via Twilio. SID: {Sid}", twilioMessage.Sid);
                return ProviderResult.Ok(twilioMessage.Sid);
            }

            _logger.LogError("Erro ao enviar SMS via Twilio. Status: {Status}, ErrorCode: {ErrorCode}", twilioMessage.Status, twilioMessage.ErrorCode);
            return ProviderResult.Fail($"SMS status: {twilioMessage.Status}", twilioMessage.ErrorCode?.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção ao enviar SMS via Twilio");
            return ProviderResult.Fail(ex.Message);
        }
    }

    private Uri? BuildStatusCallbackUrl(string? correlationId)
    {
        var publicBaseUrl = _configuration["WebhookSettings:PublicBaseUrl"];
        var sharedSecret = _configuration["WebhookSettings:SharedSecret"];

        if (string.IsNullOrEmpty(publicBaseUrl) || string.IsNullOrEmpty(sharedSecret) || string.IsNullOrEmpty(correlationId))
        {
            return null;
        }

        var url = $"{publicBaseUrl.TrimEnd('/')}/api/v1/webhooks/twilio?token={Uri.EscapeDataString(sharedSecret)}&dispatchId={Uri.EscapeDataString(correlationId)}";
        return new Uri(url);
    }
}
