using System.Text.RegularExpressions;

namespace NotifyMessages.Application.Validators;

// Regra única de "contacto válido" (e-mail ou telefone), usada pelo envio individual e pelos lotes.
public static partial class ContactRules
{
    public static bool IsValid(string? contact)
        => !string.IsNullOrWhiteSpace(contact) && contact.Length <= 100
           && (EmailRegex().IsMatch(contact) || PhoneRegex().IsMatch(contact));

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^\+?[0-9]{7,15}$")]
    private static partial Regex PhoneRegex();
}
