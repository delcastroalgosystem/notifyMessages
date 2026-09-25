namespace NotifyMessages.Application.DTOs;

public class TemplatePreviewResultDto
{
    public string? Subject { get; set; }
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
    public IReadOnlyList<string> MissingVariables { get; set; } = Array.Empty<string>();
}
