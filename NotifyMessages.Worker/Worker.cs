using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Application.Options;
using NotifyMessages.Application.Services;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;
using NotifyMessages.Infrastructure.Persistence;

namespace NotifyMessages.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly WorkerOptions _workerOptions;

    public Worker(
        ILogger<Worker> logger, 
        IServiceScopeFactory serviceScopeFactory,
        IOptions<WorkerOptions> workerOptions)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _workerOptions = workerOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker iniciado. PollingInterval: {PollingInterval}ms, BatchSize: {BatchSize}, MaxParallelism: {MaxParallelism}",
            _workerOptions.PollingIntervalMs, _workerOptions.BatchSize, _workerOptions.MaxDegreeOfParallelism);

        var semaphore = new SemaphoreSlim(_workerOptions.MaxDegreeOfParallelism);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                List<long> messageIds;
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var agora = DateTime.UtcNow;
                    messageIds = await dbContext.MessageDispatches
                        .Where(m => m.CurrentStatus == DispatchStatus.Queued)
                        .Where(m => m.ScheduledAt == null || m.ScheduledAt <= agora)
                        // Tenant com o envio desligado: as mensagens esperam na fila
                        .Where(m => dbContext.Tenants.Any(t => t.Id == m.TenantId && t.SendingEnabled))
                        .OrderBy(m => m.CreatedAt)
                        .Take(_workerOptions.BatchSize)
                        .Select(m => m.Id)
                        .ToListAsync(stoppingToken);
                }

                if (messageIds.Any())
                {
                    _logger.LogInformation("Processando {Count} mensagens", messageIds.Count);

                    // Um scope (e DbContext) por mensagem: o DbContext não é thread-safe
                    var tasks = messageIds.Select(async messageId =>
                    {
                        await semaphore.WaitAsync(stoppingToken);
                        try
                        {
                            using var messageScope = _serviceScopeFactory.CreateScope();
                            var messageContext = messageScope.ServiceProvider.GetRequiredService<AppDbContext>();

                            var message = await messageContext.MessageDispatches
                                .FirstOrDefaultAsync(m => m.Id == messageId && m.CurrentStatus == DispatchStatus.Queued, stoppingToken);
                            if (message == null)
                            {
                                return;
                            }

                            await ProcessMessageAsync(message, messageScope.ServiceProvider, stoppingToken);
                            await messageContext.SaveChangesAsync(stoppingToken);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger.LogError(ex, "Erro ao gravar o resultado da mensagem {Id}", messageId);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar mensagens");
            }

            await Task.Delay(_workerOptions.PollingIntervalMs, stoppingToken);
        }
    }

    private async Task ProcessMessageAsync(MessageDispatch message, IServiceProvider services, CancellationToken ct)
    {
        try
        {
            message.CurrentStatus = DispatchStatus.Processing;

            var providerFactory = services.GetRequiredService<IProviderFactory>();
            var dbContext = services.GetRequiredService<AppDbContext>();

            var template = await dbContext.Templates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == message.TemplateId, ct);

            if (template == null)
            {
                message.RetryCount++;
                message.ErrorLog = $"Template {message.TemplateId} não encontrado";
                message.CurrentStatus = message.RetryCount >= _workerOptions.MaxRetries
                    ? DispatchStatus.Failed
                    : DispatchStatus.Queued;
                return;
            }

            if (!template.IsActive)
            {
                message.CurrentStatus = DispatchStatus.Canceled;
                message.ErrorLog = "Template inativo";
                return;
            }

            // Sandbox: o tenant tem endereço de teste, ou o Sandbox é forçado (fora de Produção)
            var sandboxContact = await dbContext.Tenants
                .AsNoTracking()
                .Where(t => t.Id == message.TenantId)
                .Select(t => t.SandboxContact)
                .FirstOrDefaultAsync(ct);
            bool sandbox = !string.IsNullOrWhiteSpace(sandboxContact) || _workerOptions.ForceSandbox == true;
            if (sandbox && string.IsNullOrWhiteSpace(sandboxContact))
            {
                message.CurrentStatus = DispatchStatus.Failed;
                message.ErrorLog = "Sandbox forçado (fora de Produção) e o tenant não tem SANDBOX_CONTACT: nada foi enviado";
                return;
            }
            if (sandbox && template.Channel != ChannelType.Email)
            {
                message.CurrentStatus = DispatchStatus.Canceled;
                message.ErrorLog = "Sandbox só redireciona e-mails: SMS não enviado";
                return;
            }
            string destino = sandbox ? sandboxContact!.Trim() : message.RecipientContact;
            message.SentTo = destino;

            var providerConfig = providerFactory.GetProviderConfig(message.TenantId, template.ProviderType);

            ProviderResult result;

            if (template.UseCampaignMode && template.Channel == ChannelType.Email)
            {
                var provider = providerFactory.GetCampaignProvider(template.ProviderType);
                var campaignRequest = BuildCampaignRequest(message, destino, template, providerConfig);
                result = await provider.CreateAndSendCampaignAsync(campaignRequest, providerConfig, ct);
            }
            else if (template.Channel == ChannelType.Email)
            {
                var provider = providerFactory.GetEmailProvider(template.ProviderType);
                var emailMessage = BuildEmailMessage(message, destino, template, providerConfig);
                result = await provider.SendAsync(emailMessage, providerConfig, ct);
            }
            else
            {
                var provider = providerFactory.GetSmsProvider(template.ProviderType);
                var smsMessage = BuildSmsMessage(message, destino, template, providerConfig);
                result = await provider.SendAsync(smsMessage, providerConfig, ct);
            }

            if (result.Success)
            {
                message.CurrentStatus = DispatchStatus.Sent;
                message.ExternalId = result.ExternalId;
                message.ProcessedAt = DateTime.UtcNow;
                _logger.LogInformation("Mensagem {Id} enviada com sucesso. ExternalId: {ExternalId}", message.Id, result.ExternalId);
            }
            else
            {
                message.RetryCount++;
                message.ErrorLog = $"[{result.ErrorCode}] {result.ErrorMessage}";
                message.CurrentStatus = message.RetryCount >= _workerOptions.MaxRetries
                    ? DispatchStatus.Failed
                    : DispatchStatus.Queued;
                _logger.LogWarning("Mensagem {Id} falhou. Retry {RetryCount}/{MaxRetries}. Error: {Error}",
                    message.Id, message.RetryCount, _workerOptions.MaxRetries, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            message.RetryCount++;
            message.ErrorLog = ex.Message;
            message.CurrentStatus = message.RetryCount >= _workerOptions.MaxRetries
                ? DispatchStatus.Failed
                : DispatchStatus.Queued;
            _logger.LogError(ex, "Exceção ao processar mensagem {Id}", message.Id);
        }
    }

    private EmailMessage BuildEmailMessage(MessageDispatch message, string destino, Template template, ProviderConfig providerConfig)
    {
        var contextData = message.GetContextData<Dictionary<string, string>>() ?? new();
        var htmlBody = ReplaceVariables(template.HtmlBody ?? string.Empty, contextData);
        var subject = ReplaceVariables(template.Subject ?? string.Empty, contextData);

        return new EmailMessage
        {
            To = destino,
            ToName = message.RecipientName,
            From = template.SenderId ?? providerConfig.SenderId ?? string.Empty,
            FromName = template.SenderName ?? providerConfig.SenderName ?? string.Empty,
            Subject = subject,
            HtmlBody = htmlBody,
            OpenTracking = false,
            ClickTracking = false,
            CorrelationId = message.Id.ToString()
        };
    }

    private SmsMessage BuildSmsMessage(MessageDispatch message, string destino, Template template, ProviderConfig providerConfig)
    {
        var contextData = message.GetContextData<Dictionary<string, string>>() ?? new();
        var textBody = ReplaceVariables(template.TextBody ?? string.Empty, contextData);

        return new SmsMessage
        {
            To = destino,
            From = template.SenderId ?? providerConfig.SenderId ?? providerConfig.FromNumber ?? string.Empty,
            TextBody = textBody,
            MaxParts = 5,
            CorrelationId = message.Id.ToString()
        };
    }

    private CampaignRequest BuildCampaignRequest(MessageDispatch message, string destino, Template template, ProviderConfig providerConfig)
    {
        var contextData = message.GetContextData<Dictionary<string, string>>() ?? new();
        var htmlBody = ReplaceVariables(template.HtmlBody ?? string.Empty, contextData);
        var subject = ReplaceVariables(template.Subject ?? string.Empty, contextData);

        return new CampaignRequest
        {
            CampaignName = $"{template.Name}_{DateTime.UtcNow:yyyyMMddHHmmss}",
            Subject = subject,
            HtmlBody = htmlBody,
            FromEmail = template.SenderId ?? providerConfig.SenderId ?? string.Empty,
            FromName = template.SenderName ?? providerConfig.SenderName ?? string.Empty,
            ListId = template.ListId ?? 0,
            Contacts = new List<CampaignContact>
            {
                new()
                {
                    Email = destino,
                    Name = message.RecipientName,
                    CustomFields = contextData
                }
            },
            OpenTracking = false,
            ClickTracking = false
        };
    }

    private static string ReplaceVariables(string template, Dictionary<string, string> variables)
        => TemplateVariableRenderer.Render(template, variables);
}
