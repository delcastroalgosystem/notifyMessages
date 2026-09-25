using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotifyMessages.Application.Security;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, ILogger? logger = null)
    {
        if (await context.Tenants.AnyAsync())
        {
            return;
        }

        var tenants = new List<Tenant>
        {
            new() { Name = "Clube Desportivo Exemplo", DocumentId = "500123456", Active = true },
            new() { Name = "Contabilidade Exemplo & Associados, Lda", DocumentId = "PT509876543", Active = true },
            new() { Name = "Imobiliária Exemplo", DocumentId = "PT512345678", Active = true }
        };

        var plainApiKeys = new Dictionary<Tenant, string>();
        foreach (var tenant in tenants)
        {
            var apiKey = ApiKeyHasher.GenerateApiKey();
            tenant.ApiKeyHash = ApiKeyHasher.Hash(apiKey);
            plainApiKeys[tenant] = apiKey;
        }

        await context.Tenants.AddRangeAsync(tenants);
        await context.SaveChangesAsync();

        foreach (var (tenant, apiKey) in plainApiKeys)
        {
            logger?.LogWarning(
                "Seed: API Key gerada para Tenant {TenantId} ({TenantName}): {ApiKey} — guarde-a agora, apenas o hash fica persistido",
                tenant.Id, tenant.Name, apiKey);
        }

        var templates = new List<Template>
        {
            new()
            {
                Name = "Boas-Vindas Email",
                Channel = ChannelType.Email,
                ProviderType = ProviderType.SendGrid,
                UseCampaignMode = false,
                Subject = "Bem-vindo(a), {{Nome}}!",
                HtmlBody = "<p>Olá {{Nome}}, bem-vindo(a) a {{Empresa}}.</p>",
                TextBody = "Olá {{Nome}}, bem-vindo(a) a {{Empresa}}.",
                SenderName = "NotifyMessages",
                IsActive = true
            },
            new()
            {
                Name = "Aviso de Cobrança SMS",
                Channel = ChannelType.Sms,
                ProviderType = ProviderType.Twilio,
                UseCampaignMode = false,
                TextBody = "Olá {{Nome}}, tem uma fatura de {{Valor}} pendente. Pague até {{DataLimite}}.",
                IsActive = true
            },
            new()
            {
                Name = "Notificação Egoi Email",
                Channel = ChannelType.Email,
                ProviderType = ProviderType.Egoi,
                UseCampaignMode = false,
                Subject = "Aviso: {{Assunto}}",
                HtmlBody = "<p>{{Mensagem}}</p>",
                TextBody = "{{Mensagem}}",
                SenderName = "NotifyMessages",
                IsActive = true
            },
            new()
            {
                Name = "Notificação Brevo Email",
                Channel = ChannelType.Email,
                ProviderType = ProviderType.Brevo,
                UseCampaignMode = false,
                Subject = "Aviso: {{Assunto}}",
                HtmlBody = "<p>{{Mensagem}}</p>",
                TextBody = "{{Mensagem}}",
                SenderName = "NotifyMessages",
                IsActive = true
            },
            new()
            {
                Name = "Aviso Brevo SMS",
                Channel = ChannelType.Sms,
                ProviderType = ProviderType.BrevoSms,
                UseCampaignMode = false,
                TextBody = "Olá {{Nome}}, tem uma fatura de {{Valor}} pendente. Pague até {{DataLimite}}.",
                IsActive = true
            }
        };
        await context.Templates.AddRangeAsync(templates);
        await context.SaveChangesAsync();

        var providerConfigs = new List<TenantProviderConfig>
        {
            new()
            {
                TenantId = tenants[0].Id,
                ProviderType = ProviderType.Twilio,
                IsGlobal = false,
                IsActive = true,
                AccountSid = "SEEDED-DEV-ACCOUNT-SID",
                AuthToken = "SEEDED-DEV-AUTH-TOKEN",
                FromNumber = "+351900000000"
            },
            new()
            {
                TenantId = tenants[1].Id,
                ProviderType = ProviderType.SendGrid,
                IsGlobal = false,
                IsActive = true,
                ApiKey = "SEEDED-DEV-API-KEY",
                SenderId = "geral@exemplo-contabilidade.pt",
                SenderName = "Contabilidade Exemplo"
            }
        };
        await context.TenantProviderConfigs.AddRangeAsync(providerConfigs);
        await context.SaveChangesAsync();
    }
}
