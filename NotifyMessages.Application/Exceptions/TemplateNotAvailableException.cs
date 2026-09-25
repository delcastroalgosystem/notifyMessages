namespace NotifyMessages.Application.Exceptions;

/// <summary>
/// O template pedido não existe, ou pertence a outro tenant (só se pode usar os seus e os partilhados).
/// </summary>
public class TemplateNotAvailableException : Exception
{
    public TemplateNotAvailableException(int templateId)
        : base($"Template {templateId} não existe ou não está disponível para este tenant.")
    {
        TemplateId = templateId;
    }

    public int TemplateId { get; }
}
