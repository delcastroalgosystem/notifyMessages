using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NotifyMessages.Application.Security;

namespace NotifyMessages.Api.Authentication;

public class AdminKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "AdminKey";
    public const string HeaderName = "X-Admin-Key";
    public const string ActingUserHeader = "X-Acting-User";
    // Hashes SHA-256 (hex) das chaves de plataforma aceites; vários para permitir a troca sem cortes.
    public const string ConfigSection = "AdminApi:KeyHashes";
}

// Chave de plataforma para a API de administração (/api/v1/admin): não pertence a nenhum tenant.
// X-Acting-User é obrigatório e fica como nome do utilizador (auditoria: CREATED_BY, APPROVED_BY...).
public class AdminKeyAuthenticationHandler : AuthenticationHandler<AdminKeyAuthenticationOptions>
{
    private readonly IConfiguration _configuration;

    public AdminKeyAuthenticationHandler(
        IOptionsMonitor<AdminKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AdminKeyAuthenticationOptions.HeaderName, out var chave) || string.IsNullOrWhiteSpace(chave))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Header '{AdminKeyAuthenticationOptions.HeaderName}' em falta."));
        }

        var hashes = _configuration.GetSection(AdminKeyAuthenticationOptions.ConfigSection).Get<string[]>() ?? [];
        if (!IsValid(chave.ToString(), hashes))
        {
            return Task.FromResult(AuthenticateResult.Fail("Chave de plataforma inválida."));
        }

        string actingUser = Request.Headers[AdminKeyAuthenticationOptions.ActingUserHeader].ToString().Trim();
        if (actingUser.Length is 0 or > 256)
        {
            return Task.FromResult(AuthenticateResult.Fail($"Header '{AdminKeyAuthenticationOptions.ActingUserHeader}' obrigatório (quem faz a ação, máx. 256 caracteres)."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, actingUser), new Claim(ClaimTypes.Role, "PlatformAdmin")], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    // Comparação em tempo constante contra cada hash configurado.
    internal static bool IsValid(string chave, IEnumerable<string> hashes)
    {
        byte[] recebido = Encoding.ASCII.GetBytes(ApiKeyHasher.Hash(chave));
        bool valido = false;
        foreach (string h in hashes.Where(h => !string.IsNullOrWhiteSpace(h)))
        {
            byte[] esperado = Encoding.ASCII.GetBytes(h.Trim().ToUpperInvariant());
            valido |= esperado.Length == recebido.Length && CryptographicOperations.FixedTimeEquals(esperado, recebido);
        }
        return valido;
    }
}
