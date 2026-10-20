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
