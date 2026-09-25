using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NotifyMessages.Application.Security;
using NotifyMessages.Infrastructure.Persistence;

namespace NotifyMessages.Api.Authentication;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly AppDbContext _context;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext context)
        : base(options, logger, encoder)
    {
        _context = context;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var apiKeyHeader) ||
            string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            return AuthenticateResult.Fail($"Header '{ApiKeyAuthenticationOptions.HeaderName}' em falta.");
        }

        var apiKeyHash = ApiKeyHasher.Hash(apiKeyHeader.ToString());

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ApiKeyHash == apiKeyHash && t.Active);

        if (tenant == null)
        {
            return AuthenticateResult.Fail("API Key inválida ou Tenant inativo.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, tenant.Id.ToString()),
            new Claim(ClaimTypes.Name, tenant.Name)
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
