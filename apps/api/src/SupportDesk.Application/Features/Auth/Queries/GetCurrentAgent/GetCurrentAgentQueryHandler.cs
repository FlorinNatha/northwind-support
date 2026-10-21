using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;

/// <summary>
/// Fetches profile information for the currently authenticated agent (AU-5).
/// </summary>
public sealed class GetCurrentAgentQueryHandler(IAgentRepository agents)
{
    public async Task<AuthAgentDto> HandleAsync(int agentId, CancellationToken ct)
    {
        var agent = await agents.GetByIdAsync(agentId, ct);

        if (agent is null || !agent.IsActive)
        {
            throw new AuthenticationFailedException("Agent account is inactive or not found.");
        }

        return new AuthAgentDto(agent.Id, agent.FullName, agent.Email);
    }
}
