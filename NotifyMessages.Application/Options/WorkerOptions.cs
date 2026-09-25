namespace NotifyMessages.Application.Options;

public class WorkerOptions
{
    public const string SectionName = "WorkerOptions";
    
    public int PollingIntervalMs { get; set; } = 5000;
    public int BatchSize { get; set; } = 100;
    public int MaxDegreeOfParallelism { get; set; } = 10;
    public int MaxRetries { get; set; } = 3;

    // Sandbox forçado: todos os e-mails vão para o TENANT.SANDBOX_CONTACT; sem esse endereço, a mensagem
    // falha em vez de ir para o destinatário real. Nulo = forçado fora de Produção (decidido no arranque).
    public bool? ForceSandbox { get; set; }
}
