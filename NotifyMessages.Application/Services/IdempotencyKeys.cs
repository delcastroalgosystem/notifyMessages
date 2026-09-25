using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NotifyMessages.Application.Services;

// Chave de idempotência gravada em MESSAGE_DISPATCH.IDEMPOTENCY_KEY. O envio individual e os lotes usam
// a mesma fórmula, para que a mesma externalKey seja duplicada venha por onde vier.
public static class IdempotencyKeys
{
    // Com chave do cliente: tenant + chave (os dados podem mudar sem criar um envio novo).
    public static string ForExternalKey(int tenantId, string externalKey)
        => Hash($"EXT|{tenantId}|{externalKey}");

    // Sem chave do cliente: tenant + template + contacto + dados (comportamento original).
    public static string ForContent(int tenantId, int templateId, string recipientContact, object? businessData)
        => Hash($"{tenantId}-{templateId}-{recipientContact}-{JsonSerializer.Serialize(businessData)}");

    private static string Hash(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
