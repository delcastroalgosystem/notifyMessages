namespace NotifyMessages.Application.DTOs;

public class TemplatePreviewRequestDto
{
    public Dictionary<string, string> Variables { get; set; } = new();
}
