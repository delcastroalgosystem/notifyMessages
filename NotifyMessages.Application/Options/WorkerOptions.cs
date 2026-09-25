namespace NotifyMessages.Application.Options;

public class WorkerOptions
{
    public const string SectionName = "WorkerOptions";
    
    public int PollingIntervalMs { get; set; } = 5000;
    public int BatchSize { get; set; } = 100;
    public int MaxDegreeOfParallelism { get; set; } = 10;
    public int MaxRetries { get; set; } = 3;
}
