namespace NotifyMessages.Application.DTOs.Providers;

public class ProviderResult
{
    public bool Success { get; set; }
    public string? ExternalId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RawResponse { get; set; }

    public static ProviderResult Ok(string? externalId = null) => new() 
    { 
        Success = true, 
        ExternalId = externalId 
    };

    public static ProviderResult Fail(string error, string? code = null) => new() 
    { 
        Success = false, 
        ErrorMessage = error, 
        ErrorCode = code 
    };
}
