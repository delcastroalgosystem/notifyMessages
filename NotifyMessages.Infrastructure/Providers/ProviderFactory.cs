using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Domain.Enums;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Infrastructure.Persistence;

namespace NotifyMessages.Infrastructure.Providers;

public class ProviderFactory : IProviderFactory
{
    private readonly IEnumerable<IEmailProvider> _emailProviders;
    private readonly IEnumerable<ISmsProvider> _smsProviders;
    private readonly IEnumerable<ICampaignProvider> _campaignProviders;
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProviderFactory> _logger;

    public ProviderFactory(
        IEnumerable<IEmailProvider> emailProviders,
        IEnumerable<ISmsProvider> smsProviders,
        IEnumerable<ICampaignProvider> campaignProviders,
        AppDbContext context,
        IConfiguration configuration,
        ILogger<ProviderFactory> logger)
    {
        _emailProviders = emailProviders;
        _smsProviders = smsProviders;
        _campaignProviders = campaignProviders;
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public IEmailProvider GetEmailProvider(ProviderType type)
    {
        var provider = _emailProviders.FirstOrDefault(p => p.Type == type);
        if (provider == null)
        {
            throw new InvalidOperationException($"Email provider '{type}' not registered");
        }
        return provider;
    }

    public ISmsProvider GetSmsProvider(ProviderType type)
    {
        var provider = _smsProviders.FirstOrDefault(p => p.Type == type);
        if (provider == null)
        {
            throw new InvalidOperationException($"SMS provider '{type}' not registered");
        }
        return provider;
    }

    public ICampaignProvider GetCampaignProvider(ProviderType type)
    {
        var provider = _campaignProviders.FirstOrDefault(p => p.Type == type);
        if (provider == null)
        {
            throw new InvalidOperationException($"Campaign provider '{type}' not registered");
        }
        return provider;
    }

    public ProviderConfig GetProviderConfig(int tenantId, ProviderType providerType)
    {
        var tenantConfig = _context.TenantProviderConfigs
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ProviderType == providerType && c.IsActive)
            .OrderByDescending(c => c.Id) // várias linhas ativas: a mais recente ganha (determinístico)
            .FirstOrDefault();

        if (tenantConfig != null && !tenantConfig.IsGlobal)
        {
            _logger.LogDebug("Usando configuração específica do Tenant {TenantId} para {ProviderType}", tenantId, providerType);
            return MapToProviderConfig(tenantConfig, providerType);
        }

        _logger.LogDebug("Usando configuração global para {ProviderType}", providerType);
        return GetGlobalProviderConfig(providerType);
    }

    private ProviderConfig MapToProviderConfig(Domain.Entities.TenantProviderConfig config, ProviderType providerType)
    {
        var (apiKey, authToken) = ResolveTenantSecrets(config);

        return new ProviderConfig
        {
            ProviderType = providerType,
            ApiKey = apiKey,
            BaseUrl = config.BaseUrl,
            Domain = config.Domain,
            SenderId = config.SenderId,
            SenderName = config.SenderName,
            AccountSid = config.AccountSid,
            AuthToken = authToken,
            FromNumber = config.FromNumber,
            ListId = config.ListId
        };
    }

    // Com SecretName, os segredos vêm da configuração (User Secrets / variáveis de ambiente / cofre).
    // Nunca cai para as credenciais globais: enviar pela conta errada é pior do que falhar.
    private (string? ApiKey, string? AuthToken) ResolveTenantSecrets(Domain.Entities.TenantProviderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.SecretName))
        {
            if (!string.IsNullOrEmpty(config.ApiKey) || !string.IsNullOrEmpty(config.AuthToken))
            {
                _logger.LogWarning("Tenant {TenantId} ({ProviderType}) usa segredo em texto simples na BD; migrar para SECRET_NAME",
                    config.TenantId, config.ProviderType);
            }
            return (config.ApiKey, config.AuthToken);
        }

        var section = $"ProviderSettings:Tenants:{config.SecretName}";
        var apiKey = _configuration[$"{section}:ApiKey"];
        var authToken = _configuration[$"{section}:AuthToken"];

        if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(authToken))
        {
            throw new InvalidOperationException(
                $"Segredo '{section}:ApiKey' (ou ':AuthToken') não encontrado na configuração para o Tenant {config.TenantId} ({config.ProviderType}).");
        }

        return (apiKey, authToken);
    }

    private ProviderConfig GetGlobalProviderConfig(ProviderType providerType)
    {
        var config = new ProviderConfig { ProviderType = providerType };

        switch (providerType)
        {
            case ProviderType.Egoi:
                config.ApiKey = _configuration["ProviderSettings:GlobalEgoi:ApiKey"];
                config.BaseUrl = _configuration["ProviderSettings:GlobalEgoi:BaseUrl"] ?? "https://slingshot.egoiapp.com/api/v2";
                config.Domain = _configuration["ProviderSettings:GlobalEgoi:Domain"];
                config.SenderId = _configuration["ProviderSettings:GlobalEgoi:SenderId"];
                config.SenderName = _configuration["ProviderSettings:GlobalEgoi:SenderName"];
                break;

            case ProviderType.EgoiCampaign:
                config.ApiKey = _configuration["ProviderSettings:GlobalEgoiCampaign:ApiKey"];
                config.BaseUrl = _configuration["ProviderSettings:GlobalEgoiCampaign:BaseUrl"] ?? "https://api.egoi.io/v3";
                config.ListId = int.TryParse(_configuration["ProviderSettings:GlobalEgoiCampaign:ListId"], out var listId) ? listId : null;
                break;

            case ProviderType.SendGrid:
                config.ApiKey = _configuration["ProviderSettings:GlobalSendGrid:ApiKey"];
                config.SenderId = _configuration["ProviderSettings:GlobalSendGrid:FromEmail"];
                config.SenderName = _configuration["ProviderSettings:GlobalSendGrid:FromName"];
                break;

            case ProviderType.Twilio:
                config.AccountSid = _configuration["ProviderSettings:GlobalTwilio:AccountSid"];
                config.AuthToken = _configuration["ProviderSettings:GlobalTwilio:AuthToken"];
                config.FromNumber = _configuration["ProviderSettings:GlobalTwilio:FromNumber"];
                break;

            case ProviderType.Brevo:
                config.ApiKey = _configuration["ProviderSettings:GlobalBrevo:ApiKey"];
                config.BaseUrl = _configuration["ProviderSettings:GlobalBrevo:BaseUrl"] ?? "https://api.brevo.com/v3";
                config.SenderId = _configuration["ProviderSettings:GlobalBrevo:FromEmail"];
                config.SenderName = _configuration["ProviderSettings:GlobalBrevo:FromName"];
                break;

            case ProviderType.BrevoSms:
                config.ApiKey = _configuration["ProviderSettings:GlobalBrevoSms:ApiKey"];
                config.BaseUrl = _configuration["ProviderSettings:GlobalBrevoSms:BaseUrl"] ?? "https://api.brevo.com/v3";
                config.SenderId = _configuration["ProviderSettings:GlobalBrevoSms:SenderId"];
                break;
        }

        return config;
    }
}
