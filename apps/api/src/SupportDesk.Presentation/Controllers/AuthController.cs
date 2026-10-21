using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Authenticates an agent and issues a JWT token (AU-4).
    /// Open endpoint: does not require authentication.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        [FromServices] LoginCommandHandler handler,
        CancellationToken ct)
    {
        var response = await handler.HandleAsync(request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns the identity of the currently signed-in agent (AU-5).
    /// Protected endpoint: enforces valid JWT bearer token.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<AuthAgentDto>> GetCurrentUser(
        [FromServices] GetCurrentAgentQueryHandler handler,
        CancellationToken ct)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(idClaim) || !int.TryParse(idClaim, out var agentId))
        {
            return Unauthorized();
        }

        var agent = await handler.HandleAsync(agentId, ct);
        return Ok(agent);
    }
}
