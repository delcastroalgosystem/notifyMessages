using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;
using NotifyMessages.Infrastructure.Persistence;
using NotifyMessages.Infrastructure.Providers;
using NotifyMessages.UnitTests.Fakes;

namespace NotifyMessages.UnitTests.Infrastructure;

public class ProviderFactoryTests
{
    private static IConfiguration BuildConfiguration(string sendGridApiKey = "global-sendgrid-key")
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProviderSettings:GlobalSendGrid:ApiKey"] = sendGridApiKey,
                ["ProviderSettings:GlobalSendGrid:FromEmail"] = "geral@notifymessages.dev",
                ["ProviderSettings:GlobalSendGrid:FromName"] = "NotifyMessages",
                ["ProviderSettings:GlobalBrevo:ApiKey"] = "global-brevo-key",
                ["ProviderSettings:GlobalBrevo:FromEmail"] = "noreply@notifymessages.dev",
                ["ProviderSettings:GlobalBrevo:FromName"] = "NotifyMessages",
                ["ProviderSettings:GlobalBrevoSms:ApiKey"] = "global-brevo-sms-key",
                ["ProviderSettings:GlobalBrevoSms:SenderId"] = "NotifyMsg"
            })
            .Build();

    private static ProviderFactory BuildFactory(
        AppDbContext context,
        IConfiguration? configuration = null,
        params FakeEmailProvider[] emailProviders)
        => new(
            emailProviders,
            Array.Empty<FakeSmsProvider>(),
            Array.Empty<FakeCampaignProvider>(),
            context,
            configuration ?? BuildConfiguration(),
            NullLogger<ProviderFactory>.Instance);

    [Fact]
    public void GetEmailProvider_ProviderRegistado_RetornaProviderCorreto()
    {
        using var context = TestDbContextFactory.Create();
        var sendGrid = new FakeEmailProvider(ProviderType.SendGrid);
        var egoi = new FakeEmailProvider(ProviderType.Egoi);
        var factory = BuildFactory(context, emailProviders: [sendGrid, egoi]);

        var resolved = factory.GetEmailProvider(ProviderType.Egoi);

        Assert.Same(egoi, resolved);
    }

    [Fact]
    public void GetEmailProvider_ProviderNaoRegistado_LancaExcecao()
    {
        using var context = TestDbContextFactory.Create();
        var factory = BuildFactory(context, emailProviders: [new FakeEmailProvider(ProviderType.SendGrid)]);

        Assert.Throws<InvalidOperationException>(() => factory.GetEmailProvider(ProviderType.Twilio));
    }

    [Fact]
    public void GetProviderConfig_SemConfigDoTenant_UsaConfigGlobalDoAppsettings()
    {
        using var context = TestDbContextFactory.Create();
        var factory = BuildFactory(context);

        var config = factory.GetProviderConfig(tenantId: 1, ProviderType.SendGrid);

        Assert.Equal("global-sendgrid-key", config.ApiKey);
        Assert.Equal("geral@notifymessages.dev", config.SenderId);
    }

    [Fact]
    public void GetProviderConfig_ComConfigAtivaEEspecificaDoTenant_UsaConfigDoTenant()
    {
        using var context = TestDbContextFactory.Create();
        context.TenantProviderConfigs.Add(new TenantProviderConfig
        {
            TenantId = 1,
            ProviderType = ProviderType.SendGrid,
            IsActive = true,
            IsGlobal = false,
            ApiKey = "tenant-specific-key",
            SenderId = "tenant@cliente.pt"
        });
        context.SaveChanges();
        var factory = BuildFactory(context);

        var config = factory.GetProviderConfig(tenantId: 1, ProviderType.SendGrid);

        Assert.Equal("tenant-specific-key", config.ApiKey);
        Assert.Equal("tenant@cliente.pt", config.SenderId);
    }

    [Fact]
    public void GetProviderConfig_ConfigDoTenantMarcadaComoGlobal_CaiParaConfigGlobalDoAppsettings()
    {
        using var context = TestDbContextFactory.Create();
        context.TenantProviderConfigs.Add(new TenantProviderConfig
        {
            TenantId = 1,
            ProviderType = ProviderType.SendGrid,
            IsActive = true,
            IsGlobal = true,
            ApiKey = "tenant-key-que-nao-deveria-ser-usada"
        });
        context.SaveChanges();
        var factory = BuildFactory(context);

        var config = factory.GetProviderConfig(tenantId: 1, ProviderType.SendGrid);

        Assert.Equal("global-sendgrid-key", config.ApiKey);
    }

    [Fact]
    public void GetProviderConfig_ConfigDoTenantInativa_CaiParaConfigGlobalDoAppsettings()
    {
        using var context = TestDbContextFactory.Create();
        context.TenantProviderConfigs.Add(new TenantProviderConfig
        {
            TenantId = 1,
            ProviderType = ProviderType.SendGrid,
            IsActive = false,
            IsGlobal = false,
            ApiKey = "tenant-key-inativa"
        });
        context.SaveChanges();
        var factory = BuildFactory(context);

        var config = factory.GetProviderConfig(tenantId: 1, ProviderType.SendGrid);

        Assert.Equal("global-sendgrid-key", config.ApiKey);
    }

    [Fact]
    public void GetProviderConfig_ConfigDeOutroTenant_NaoInterfereNaResolucao()
    {
        using var context = TestDbContextFactory.Create();
        context.TenantProviderConfigs.Add(new TenantProviderConfig
        {
            TenantId = 2,
            ProviderType = ProviderType.SendGrid,
            IsActive = true,
            IsGlobal = false,
            ApiKey = "config-do-tenant-2"
        });
        context.SaveChanges();
        var factory = BuildFactory(context);

        var config = factory.GetProviderConfig(tenantId: 1, ProviderType.SendGrid);

        Assert.Equal("global-sendgrid-key", config.ApiKey);
    }

    [Fact]
    public void GetProviderConfig_Brevo_SemConfigDoTenant_UsaConfigGlobalDoAppsettings()
    {
        using var context = TestDbContextFactory.Create();
        var factory = BuildFactory(context);

        var config = factory.GetProviderConfig(tenantId: 1, ProviderType.Brevo);

        Assert.Equal("global-brevo-key", config.ApiKey);
        Assert.Equal("noreply@notifymessages.dev", config.SenderId);
    }

    [Fact]
    public void GetProviderConfig_BrevoSms_SemConfigDoTenant_UsaConfigGlobalDoAppsettings()
    {
        using var context = TestDbContextFactory.Create();
        var factory = BuildFactory(context);

        var config = factory.GetProviderConfig(tenantId: 1, ProviderType.BrevoSms);

        Assert.Equal("global-brevo-sms-key", config.ApiKey);
        Assert.Equal("NotifyMsg", config.SenderId);
    }

    [Fact]
    public void GetProviderConfig_TenantComSecretName_LeChaveDaConfiguracaoENaoDaBd()
    {
        using var context = TestDbContextFactory.Create();
        context.TenantProviderConfigs.Add(new TenantProviderConfig
        {
            TenantId = 1,
            ProviderType = ProviderType.Egoi,
            IsActive = true,
            IsGlobal = false,
            SecretName = "CLUBE_AAC_Egoi",
            ApiKey = "chave-antiga-na-bd",
            SenderId = "5"
        });
        context.SaveChanges();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProviderSettings:Tenants:CLUBE_AAC_Egoi:ApiKey"] = "chave-da-configuracao"
            })
            .Build();
        var factory = BuildFactory(context, configuration);

        var config = factory.GetProviderConfig(tenantId: 1, ProviderType.Egoi);

        Assert.Equal("chave-da-configuracao", config.ApiKey);
        Assert.Equal("5", config.SenderId);
    }

    [Fact]
    public void GetProviderConfig_SecretNameSemValorNaConfiguracao_LancaExcecaoENaoUsaGlobal()
    {
        using var context = TestDbContextFactory.Create();
        context.TenantProviderConfigs.Add(new TenantProviderConfig
        {
            TenantId = 1,
            ProviderType = ProviderType.SendGrid,
            IsActive = true,
            IsGlobal = false,
            SecretName = "CLUBE_SEM_SEGREDO"
        });
        context.SaveChanges();
        var factory = BuildFactory(context);

        var ex = Assert.Throws<InvalidOperationException>(() => factory.GetProviderConfig(tenantId: 1, ProviderType.SendGrid));
        Assert.Contains("ProviderSettings:Tenants:CLUBE_SEM_SEGREDO", ex.Message);
    }

    [Fact]
    public void GetProviderConfig_VariasConfigsAtivasDoTenant_UsaAMaisRecente()
    {
        using var context = TestDbContextFactory.Create();
        context.TenantProviderConfigs.Add(new TenantProviderConfig { Id = 1, TenantId = 1, ProviderType = ProviderType.SendGrid, IsActive = true, ApiKey = "antiga" });
        context.TenantProviderConfigs.Add(new TenantProviderConfig { Id = 2, TenantId = 1, ProviderType = ProviderType.SendGrid, IsActive = true, ApiKey = "recente" });
        context.SaveChanges();
        var factory = BuildFactory(context);

        var config = factory.GetProviderConfig(tenantId: 1, ProviderType.SendGrid);

        Assert.Equal("recente", config.ApiKey);
    }

    [Fact]
    public void GetEmailProvider_Brevo_ENaoConfundeComEmailDoEgoi()
    {
        using var context = TestDbContextFactory.Create();
        var egoi = new FakeEmailProvider(ProviderType.Egoi);
        var brevo = new FakeEmailProvider(ProviderType.Brevo);
        var factory = BuildFactory(context, emailProviders: [egoi, brevo]);

        var resolved = factory.GetEmailProvider(ProviderType.Brevo);

        Assert.Same(brevo, resolved);
    }
}
