using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotifyMessages.Api.Authentication;

namespace NotifyMessages.Api.Controllers.Admin;

// Base da API de administração: só a chave de plataforma (X-Admin-Key) entra; a X-Api-Key de um tenant não.
[ApiController]
[Authorize(AuthenticationSchemes = AdminKeyAuthenticationOptions.SchemeName)]
public abstract class AdminControllerBase : ControllerBase
{
    // Quem fez a ação (X-Acting-User), para auditoria.
    protected string ActingUser => User.FindFirstValue(ClaimTypes.Name)!;
}
