namespace NotifyMessages.Application.DTOs;

// Gatilho que o conector deve correr agora (GET /api/v1/triggers/due).
public class DueTriggerDto
{
    public int TriggerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int TemplateId { get; set; }
    // Dia de execução na hora local do tenant: é o RunDate a enviar no lote.
    public DateOnly RunDate { get; set; }
    public string? Parameters { get; set; }
    public bool RequiresApproval { get; set; }
}
