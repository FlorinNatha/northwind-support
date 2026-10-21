using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>
/// Authenticates an agent using email and password, returning a signed JWT (AU-4).
/// Rejects unrecognised emails, wrong passwords, and inactive agents with an identical 401 error.
/// </summary>
public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    ILogger<LoginCommandHandler> logger)
{
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);

        // Security requirement AU-4: identical 401 response and message for invalid email,
        // invalid password, or inactive agent (mitigates account enumeration).
        if (agent is null)
        {
            logger.LogWarning("Failed login attempt for non-existent email: {Email}", request.Email);
            throw new AuthenticationFailedException();
        }

        if (!agent.IsActive)
        {
            logger.LogWarning("Failed login attempt for inactive agent: {Email}", request.Email);
            throw new AuthenticationFailedException();
        }

        if (string.IsNullOrWhiteSpace(agent.PasswordHash) ||
            !passwordHasher.VerifyPassword(agent.PasswordHash, request.Password))
        {
            logger.LogWarning("Failed login attempt due to invalid password for agent: {Email}", request.Email);
            throw new AuthenticationFailedException();
        }

        var (token, expiresAtUtc) = jwtTokenService.GenerateToken(agent.Id, agent.Email, agent.FullName);

        logger.LogInformation("Agent {FullName} ({Email}) logged in successfully.", agent.FullName, agent.Email);

        return new LoginResponse(
            token,
            expiresAtUtc,
            new AuthAgentDto(agent.Id, agent.FullName, agent.Email));
    }
}
