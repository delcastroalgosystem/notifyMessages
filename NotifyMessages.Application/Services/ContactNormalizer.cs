namespace NotifyMessages.Application.Services;

// Forma única de comparar contactos (supressões, Sandbox): e-mails sem espaços e em minúsculas,
// telefones sem espaços. Não valida - isso é do validador de cada pedido.
public static class ContactNormalizer
{
    public static string Normalize(string contact)
    {
        var trimmed = contact.Trim();
        return trimmed.Contains('@') ? trimmed.ToLowerInvariant() : trimmed.Replace(" ", string.Empty);
    }
}
