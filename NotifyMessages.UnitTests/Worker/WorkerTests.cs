using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Application.Options;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;
using NotifyMessages.Infrastructure.Persistence;
using NotifyMessages.Infrastructure.Providers;
using NotifyMessages.UnitTests.Fakes;
using WorkerService = NotifyMessages.Worker.Worker;

namespace NotifyMessages.UnitTests.Worker;

public class WorkerTests
{
    private static (ServiceProvider Provider, FakeEmailProvider EmailProvider) BuildServiceProvider(
        string databaseName, WorkerOptions workerOptions, ProviderType providerType = ProviderType.SendGrid)
    {
        var emailProvider = new FakeEmailProvider(providerType);

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IEmailProvider>(emailProvider);
        services.AddScoped<IProviderFactory, ProviderFactory>();
        services.AddSingleton(Options.Create(workerOptions));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();

        return (services.BuildServiceProvider(), emailProvider);
    }

    private static WorkerOptions FastPolling() => new()
    {
        PollingIntervalMs = 50,
        BatchSize = 10,
        MaxDegreeOfParallelism = 2,
        MaxRetries = 1
    };

    [Fact]
    public async Task Worker_MensagemValida_MarcaComoSentEGravaExternalId()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, emailProvider) = BuildServiceProvider(dbName, FastPolling());
        emailProvider.Handler = (_, _) => ProviderResult.Ok("EXT-123");

        using (var seedContext = TestDbContextFactory.Create(dbName))
        {
            seedContext.Tenants.Add(new Tenant { Id = 1, Name = "Tenant" });
            seedContext.Templates.Add(new Template { Id = 1, Name = "T", Channel = ChannelType.Email, ProviderType = ProviderType.SendGrid, IsActive = true, SenderId = "from@x.pt", Subject = "Ola", HtmlBody = "<p>Ola</p>" });
            seedContext.MessageDispatches.Add(new MessageDispatch { TenantId = 1, TemplateId = 1, IdempotencyKey = "k1", RecipientName = "Cliente", RecipientContact = "cliente@x.pt", CurrentStatus = DispatchStatus.Queued });
            seedContext.SaveChanges();
        }

        var worker = new WorkerService(NullLogger<WorkerService>.Instance, provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<IOptions<WorkerOptions>>());

        await RunWorkerBrieflyAsync(worker);

        using var assertContext = TestDbContextFactory.Create(dbName);
        var message = await assertContext.MessageDispatches.SingleAsync();
        Assert.Equal(DispatchStatus.Sent, message.CurrentStatus);
        Assert.Equal("EXT-123", message.ExternalId);
        Assert.Equal(1, emailProvider.CallCount);
    }

    [Fact]
    public async Task Worker_ProviderFalhaSempre_MarcaComoFailedAoEsgotarRetries()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = FastPolling();
        var (provider, emailProvider) = BuildServiceProvider(dbName, options);
        emailProvider.Handler = (_, _) => ProviderResult.Fail("Erro simulado do provedor", "ERR");

        using (var seedContext = TestDbContextFactory.Create(dbName))
        {
            seedContext.Tenants.Add(new Tenant { Id = 1, Name = "Tenant" });
            seedContext.Templates.Add(new Template { Id = 1, Name = "T", Channel = ChannelType.Email, ProviderType = ProviderType.SendGrid, IsActive = true, SenderId = "from@x.pt", Subject = "Ola", HtmlBody = "<p>Ola</p>" });
            seedContext.MessageDispatches.Add(new MessageDispatch { TenantId = 1, TemplateId = 1, IdempotencyKey = "k1", RecipientName = "Cliente", RecipientContact = "cliente@x.pt", CurrentStatus = DispatchStatus.Queued });
            seedContext.SaveChanges();
        }

        var worker = new WorkerService(NullLogger<WorkerService>.Instance, provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<IOptions<WorkerOptions>>());

        // MaxRetries=1: primeiro ciclo falha e RetryCount chega a 1 => Failed já nesse ciclo
        await RunWorkerBrieflyAsync(worker);

        using var assertContext = TestDbContextFactory.Create(dbName);
        var message = await assertContext.MessageDispatches.SingleAsync();
        Assert.Equal(DispatchStatus.Failed, message.CurrentStatus);
        Assert.Equal(1, message.RetryCount);
        Assert.Contains("Erro simulado do provedor", message.ErrorLog);
    }

    [Fact]
    public async Task Worker_TemplateInativo_CancelaMensagemSemChamarProvider()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, emailProvider) = BuildServiceProvider(dbName, FastPolling());

        using (var seedContext = TestDbContextFactory.Create(dbName))
        {
            seedContext.Tenants.Add(new Tenant { Id = 1, Name = "Tenant" });
            seedContext.Templates.Add(new Template { Id = 1, Name = "T", Channel = ChannelType.Email, ProviderType = ProviderType.SendGrid, IsActive = false });
            seedContext.MessageDispatches.Add(new MessageDispatch { TenantId = 1, TemplateId = 1, IdempotencyKey = "k1", RecipientName = "Cliente", RecipientContact = "cliente@x.pt", CurrentStatus = DispatchStatus.Queued });
            seedContext.SaveChanges();
        }

        var worker = new WorkerService(NullLogger<WorkerService>.Instance, provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<IOptions<WorkerOptions>>());

        await RunWorkerBrieflyAsync(worker);

        using var assertContext = TestDbContextFactory.Create(dbName);
        var message = await assertContext.MessageDispatches.SingleAsync();
        Assert.Equal(DispatchStatus.Canceled, message.CurrentStatus);
        Assert.Equal(0, emailProvider.CallCount);
    }

    [Fact]
    public async Task Worker_VariasMensagensEmParalelo_EnviaTodasEGravaCadaResultado()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = FastPolling();
        options.MaxDegreeOfParallelism = 5;
        var (provider, emailProvider) = BuildServiceProvider(dbName, options);
        emailProvider.Handler = (msg, _) => ProviderResult.Ok($"EXT-{msg.To}");

        using (var seedContext = TestDbContextFactory.Create(dbName))
        {
            seedContext.Tenants.Add(new Tenant { Id = 1, Name = "Tenant" });
            seedContext.Templates.Add(new Template { Id = 1, Name = "T", Channel = ChannelType.Email, ProviderType = ProviderType.SendGrid, IsActive = true, SenderId = "from@x.pt", Subject = "Ola", HtmlBody = "<p>Ola</p>" });
            for (var i = 1; i <= 8; i++)
            {
                seedContext.MessageDispatches.Add(new MessageDispatch { TenantId = 1, TemplateId = 1, IdempotencyKey = $"k{i}", RecipientName = "Cliente", RecipientContact = $"cliente{i}@x.pt", CurrentStatus = DispatchStatus.Queued });
            }
            seedContext.SaveChanges();
        }

        var worker = new WorkerService(NullLogger<WorkerService>.Instance, provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<IOptions<WorkerOptions>>());

        await RunWorkerBrieflyAsync(worker);

        using var assertContext = TestDbContextFactory.Create(dbName);
        var messages = await assertContext.MessageDispatches.ToListAsync();
        Assert.All(messages, m =>
        {
            Assert.Equal(DispatchStatus.Sent, m.CurrentStatus);
            Assert.Equal($"EXT-{m.RecipientContact}", m.ExternalId);
        });
        Assert.Equal(8, emailProvider.CallCount);
    }

    private static void Seed(string dbName, Tenant tenant, MessageDispatch dispatch)
    {
        using var seedContext = TestDbContextFactory.Create(dbName);
        seedContext.Tenants.Add(tenant);
        seedContext.Templates.Add(new Template { Id = 1, Name = "T", Channel = ChannelType.Email, ProviderType = ProviderType.SendGrid, IsActive = true, SenderId = "from@x.pt", Subject = "Ola", HtmlBody = "<p>Ola</p>" });
        seedContext.MessageDispatches.Add(dispatch);
        seedContext.SaveChanges();
    }

    private static MessageDispatch Mensagem(DateTime? scheduledAt = null) => new()
    {
        TenantId = 1, TemplateId = 1, IdempotencyKey = "k1", RecipientName = "Socio", RecipientContact = "socio.real@x.pt",
        CurrentStatus = DispatchStatus.Queued, ScheduledAt = scheduledAt
    };

    private static WorkerService CriarWorker(ServiceProvider provider) =>
        new(NullLogger<WorkerService>.Instance, provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<IOptions<WorkerOptions>>());

    [Fact]
    public async Task Worker_TenantComSandboxContact_EnviaParaOEnderecoDeTesteEGravaSentTo()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, emailProvider) = BuildServiceProvider(dbName, FastPolling());
        string? destino = null;
        emailProvider.Handler = (msg, _) => { destino = msg.To; return ProviderResult.Ok("EXT-1"); };
        Seed(dbName, new Tenant { Id = 1, Name = "T", SandboxContact = "teste@empresa.pt" }, Mensagem());

        await RunWorkerBrieflyAsync(CriarWorker(provider));

        using var assertContext = TestDbContextFactory.Create(dbName);
        var message = await assertContext.MessageDispatches.SingleAsync();
        Assert.Equal("teste@empresa.pt", destino);
        Assert.Equal("teste@empresa.pt", message.SentTo);
        Assert.Equal("socio.real@x.pt", message.RecipientContact);
        Assert.Equal(DispatchStatus.Sent, message.CurrentStatus);
    }

    [Fact]
    public async Task Worker_SandboxForcadoSemEndereco_FalhaSemChamarOProvedor()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = FastPolling();
        options.ForceSandbox = true;
        var (provider, emailProvider) = BuildServiceProvider(dbName, options);
        Seed(dbName, new Tenant { Id = 1, Name = "T" }, Mensagem());

        await RunWorkerBrieflyAsync(CriarWorker(provider));

        using var assertContext = TestDbContextFactory.Create(dbName);
        var message = await assertContext.MessageDispatches.SingleAsync();
        Assert.Equal(DispatchStatus.Failed, message.CurrentStatus);
        Assert.Contains("SANDBOX_CONTACT", message.ErrorLog);
        Assert.Equal(0, emailProvider.CallCount);
    }

    [Fact]
    public async Task Worker_TenantComEnvioDesligado_MensagemFicaEmFila()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, emailProvider) = BuildServiceProvider(dbName, FastPolling());
        Seed(dbName, new Tenant { Id = 1, Name = "T", SendingEnabled = false }, Mensagem());

        await RunWorkerBrieflyAsync(CriarWorker(provider));

        using var assertContext = TestDbContextFactory.Create(dbName);
        Assert.Equal(DispatchStatus.Queued, (await assertContext.MessageDispatches.SingleAsync()).CurrentStatus);
        Assert.Equal(0, emailProvider.CallCount);
    }

    [Fact]
    public async Task Worker_ScheduledAtNoFuturo_NaoEnviaAinda()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, emailProvider) = BuildServiceProvider(dbName, FastPolling());
        Seed(dbName, new Tenant { Id = 1, Name = "T" }, Mensagem(scheduledAt: DateTime.UtcNow.AddHours(1)));

        await RunWorkerBrieflyAsync(CriarWorker(provider));

        using var assertContext = TestDbContextFactory.Create(dbName);
        Assert.Equal(DispatchStatus.Queued, (await assertContext.MessageDispatches.SingleAsync()).CurrentStatus);
        Assert.Equal(0, emailProvider.CallCount);
    }

    [Fact]
    public async Task Worker_ScheduledAtNoPassado_Envia()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, emailProvider) = BuildServiceProvider(dbName, FastPolling());
        Seed(dbName, new Tenant { Id = 1, Name = "T" }, Mensagem(scheduledAt: DateTime.UtcNow.AddMinutes(-1)));

        await RunWorkerBrieflyAsync(CriarWorker(provider));

        using var assertContext = TestDbContextFactory.Create(dbName);
        Assert.Equal(DispatchStatus.Sent, (await assertContext.MessageDispatches.SingleAsync()).CurrentStatus);
        Assert.Equal(1, emailProvider.CallCount);
    }

    private static async Task RunWorkerBrieflyAsync(WorkerService worker)
    {
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(500);
        await worker.StopAsync(CancellationToken.None);
    }
}
