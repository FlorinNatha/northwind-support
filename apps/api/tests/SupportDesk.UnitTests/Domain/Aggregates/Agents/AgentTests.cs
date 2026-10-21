using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Agents;

public class AgentTests
{
    [Fact]
    public void SetPasswordHash_ValidHash_SetsPasswordHashProperty()
    {
        var agent = new Agent("Test Agent", "test@example.com", 5, FixedClock.DefaultNow);
        const string hash = "AQAAAAIAAYagAAAAENhashValue";

        agent.SetPasswordHash(hash);

        Assert.Equal(hash, agent.PasswordHash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetPasswordHash_NullOrWhiteSpace_ThrowsArgumentException(string? invalidHash)
    {
        var agent = new Agent("Test Agent", "test@example.com", 5, FixedClock.DefaultNow);

        Assert.ThrowsAny<ArgumentException>(() => agent.SetPasswordHash(invalidHash!));
    }
}
