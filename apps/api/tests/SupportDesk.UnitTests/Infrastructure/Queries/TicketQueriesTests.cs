using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;
using Xunit;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SupportDbContext _db;
    private readonly FixedClock _clock;
    private readonly TicketQueries _queries;

    public TicketQueriesTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new SupportDbContext(options);
        _db.Database.EnsureCreated();

        _clock = new FixedClock();
        _queries = new TicketQueries(_db, _clock);

        SeedReferenceData();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetPagedAsync_FiltersExactPriority()
    {
        // Arrange
        var lowTicket = new TicketBuilder().WithReference("TCK-0001").WithPriority(TicketPriority.Low).Build();
        var highTicket = new TicketBuilder().WithReference("TCK-0002").WithPriority(TicketPriority.High).Build();
        _db.Set<Ticket>().AddRange(lowTicket, highTicket);
        await _db.SaveChangesAsync();

        var query = new TicketQuery { Priority = TicketPriority.High };

        // Act
        var result = await _queries.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("TCK-0002", result.Items[0].Reference);
        Assert.Equal(TicketPriority.High, result.Items[0].Priority);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_CombinesMultipleFiltersWithAnd()
    {
        // Arrange
        // Ticket 1: Cat 1, Cust 1, High, InProgress
        var ticket1 = new TicketBuilder().WithReference("TCK-0001").WithPriority(TicketPriority.High).WithStatus(TicketStatus.InProgress).Build();
        // Ticket 2: Cat 1, Cust 1, High, New
        var ticket2 = new TicketBuilder().WithReference("TCK-0002").WithPriority(TicketPriority.High).WithStatus(TicketStatus.New).Build();
        // Ticket 3: Cat 1, Cust 1, Low, InProgress
        var ticket3 = new TicketBuilder().WithReference("TCK-0003").WithPriority(TicketPriority.Low).WithStatus(TicketStatus.InProgress).Build();

        _db.Set<Ticket>().AddRange(ticket1, ticket2, ticket3);
        await _db.SaveChangesAsync();

        var query = new TicketQuery
        {
            Priority = TicketPriority.High,
            Status = TicketStatus.InProgress,
            CategoryId = 1
        };

        // Act
        var result = await _queries.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("TCK-0001", result.Items[0].Reference);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_SearchesCustomerName()
    {
        // Arrange
        var customer2 = new Customer("Contoso Ltd", "contact@contoso.com", null, CustomerTier.Premium, FixedClock.DefaultNow);
        _db.Set<Customer>().Add(customer2);
        await _db.SaveChangesAsync();

        var ticket1 = Ticket.Raise("TCK-0001", "Printer issue", "Printer is broken", customerId: 1, categoryId: 1, TicketPriority.Medium, FixedClock.DefaultNow);
        var ticket2 = Ticket.Raise("TCK-0002", "Network glitch", "Wi-Fi is down", customerId: customer2.Id, categoryId: 1, TicketPriority.Medium, FixedClock.DefaultNow);

        _db.Set<Ticket>().AddRange(ticket1, ticket2);
        await _db.SaveChangesAsync();

        var query = new TicketQuery { Search = "Contoso" };

        // Act
        var result = await _queries.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("TCK-0002", result.Items[0].Reference);
        Assert.Equal("Contoso Ltd", result.Items[0].Customer.Name);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersUnassignedOnly()
    {
        // Arrange
        var assigned = new TicketBuilder().WithReference("TCK-0001").AssignedTo(1).Build();
        var unassigned = new TicketBuilder().WithReference("TCK-0002").AssignedTo(null).Build();

        _db.Set<Ticket>().AddRange(assigned, unassigned);
        await _db.SaveChangesAsync();

        var query = new TicketQuery { UnassignedOnly = true };

        // Act
        var result = await _queries.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("TCK-0002", result.Items[0].Reference);
        Assert.Null(result.Items[0].AssignedAgent);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_CalculatesTotalCountAndPagesBeforePaging()
    {
        // Arrange
        for (var i = 1; i <= 5; i++)
        {
            var ticket = new TicketBuilder()
                .WithReference($"TCK-{i:D4}")
                .WithPriority(TicketPriority.Critical)
                .Build();
            _db.Set<Ticket>().Add(ticket);
        }
        await _db.SaveChangesAsync();

        var query = new TicketQuery
        {
            Priority = TicketPriority.Critical,
            Page = 1,
            PageSize = 2
        };

        // Act
        var result = await _queries.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersBySlaStatus()
    {
        // Arrange
        var now = _clock.UtcNow;

        // t1: No due date -> NotApplicable
        var t1 = Ticket.Raise("TCK-0001", "T1", "Desc", 1, 1, TicketPriority.Low, now);

        // t2: Due in 4 hours, created now -> WithinSla (remaining = 4h / 4h = 100% > 25%)
        var t2 = Ticket.Raise("TCK-0002", "T2", "Desc", 1, 1, TicketPriority.High, now, now.AddHours(4));

        // t3: Created 9 hours ago, due in 1 hour -> AtRisk (remaining = 1h / 10h = 10% <= 25%)
        var t3 = Ticket.Raise("TCK-0003", "T3", "Desc", 1, 1, TicketPriority.High, now.AddHours(-9), now.AddHours(1));

        // t4: Due 1 hour ago -> Breached
        var t4 = Ticket.Raise("TCK-0004", "T4", "Desc", 1, 1, TicketPriority.High, now.AddHours(-5), now.AddHours(-1));

        // t5: Due in 2 hours, resolved 1 hour ago -> Met
        var t5 = Ticket.Raise("TCK-0005", "T5", "Desc", 1, 1, TicketPriority.High, now.AddHours(-2), now.AddHours(2));
        t5.ChangeStatus(TicketStatus.Resolved, now.AddHours(-1));

        _db.Set<Ticket>().AddRange(t1, t2, t3, t4, t5);
        await _db.SaveChangesAsync();

        // Act & Assert
        var atRiskResult = await _queries.GetPagedAsync(new TicketQuery { SlaStatus = SlaStatus.AtRisk }, CancellationToken.None);
        Assert.Single(atRiskResult.Items);
        Assert.Equal("TCK-0003", atRiskResult.Items[0].Reference);

        var breachedResult = await _queries.GetPagedAsync(new TicketQuery { SlaStatus = SlaStatus.Breached }, CancellationToken.None);
        Assert.Single(breachedResult.Items);
        Assert.Equal("TCK-0004", breachedResult.Items[0].Reference);

        var metResult = await _queries.GetPagedAsync(new TicketQuery { SlaStatus = SlaStatus.Met }, CancellationToken.None);
        Assert.Single(metResult.Items);
        Assert.Equal("TCK-0005", metResult.Items[0].Reference);

        var withinSlaResult = await _queries.GetPagedAsync(new TicketQuery { SlaStatus = SlaStatus.WithinSla }, CancellationToken.None);
        Assert.Single(withinSlaResult.Items);
        Assert.Equal("TCK-0002", withinSlaResult.Items[0].Reference);

        var naResult = await _queries.GetPagedAsync(new TicketQuery { SlaStatus = SlaStatus.NotApplicable }, CancellationToken.None);
        Assert.Single(naResult.Items);
        Assert.Equal("TCK-0001", naResult.Items[0].Reference);
    }

    [Fact]
    public async Task GetEscalationsAsync_ReturnsEscalationsAuditHistory()
    {
        // Arrange
        var now = _clock.UtcNow;
        var ticket = Ticket.Raise("TCK-0010", "T10", "Desc", 1, 1, TicketPriority.Medium, now, now.AddHours(24));
        _db.Set<Ticket>().Add(ticket);
        await _db.SaveChangesAsync();

        ticket.Escalate(TicketPriority.High, now.AddHours(8), 1, "Customer escalated", "Manager", now);
        await _db.SaveChangesAsync();

        // Act
        var escalations = await _queries.GetEscalationsAsync(ticket.Id, CancellationToken.None);

        // Assert
        Assert.Single(escalations);
        Assert.Equal(TicketPriority.Medium, escalations[0].FromPriority);
        Assert.Equal(TicketPriority.High, escalations[0].ToPriority);
        Assert.Equal("Customer escalated", escalations[0].Reason);
        Assert.Equal("Manager", escalations[0].EscalatedBy);
        Assert.Equal("Alex Turner", escalations[0].ToAgent?.FullName);
    }

    private void SeedReferenceData()
    {
        var customer = new Customer("Fabrikam", "help@fabrikam.com", "555-0100", CustomerTier.Standard, FixedClock.DefaultNow);
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var agent = new Agent("Alex Turner", "alex@northwind.test", 10, FixedClock.DefaultNow);

        _db.Set<Customer>().Add(customer);
        _db.Set<Category>().Add(category);
        _db.Set<Agent>().Add(agent);
        _db.SaveChanges();
    }
}
