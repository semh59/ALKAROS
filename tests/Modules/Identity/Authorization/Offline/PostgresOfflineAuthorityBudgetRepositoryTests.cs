using ALKAROS.Identity.Authorization.Offline;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Offline;

public sealed class PostgresOfflineAuthorityBudgetRepositoryTests : IClassFixture<OfflineBudgetDatabase>
{
    private readonly PostgresOfflineAuthorityBudgetRepository _repository;
    private readonly DateTimeOffset _now = new(2026, 9, 4, 18, 0, 0, TimeSpan.Zero);

    public PostgresOfflineAuthorityBudgetRepositoryTests(OfflineBudgetDatabase db)
        => _repository = new PostgresOfflineAuthorityBudgetRepository(db.DataSource);

    private static OfflineAuthorityBudgetLine[] Lines()
        =>
        [
            new OfflineAuthorityBudgetLine("bills.comp", 150m, 2),
            new OfflineAuthorityBudgetLine("bills.void", null, 1),
        ];

    [Fact]
    public async Task CreateStoresTheBudgetWithItsLinesAndBothLookupsFindIt()
    {
        var user = Guid.NewGuid();
        var session = Guid.NewGuid();

        var created = await _repository.CreateAsync(user, session, Lines(), _now, _now.AddHours(4));

        created.BudgetId.Should().NotBe(Guid.Empty);
        created.Lines.Should().HaveCount(2);

        var byId = await _repository.GetAsync(created.BudgetId);
        var bySession = await _repository.GetBySessionAsync(session);

        byId!.SessionId.Should().Be(session);
        byId.ExpiresAt.Should().Be(_now.AddHours(4));
        byId.Lines.Should().ContainSingle(l => l.PermissionCode == "bills.comp" && l.LimitAmount == 150m);
        byId.Lines.Should().ContainSingle(l => l.PermissionCode == "bills.void" && l.LimitAmount == null);
        bySession!.BudgetId.Should().Be(created.BudgetId);
    }

    [Fact]
    public async Task CreateReplacesAnyExistingBudgetForTheSameSession()
    {
        var session = Guid.NewGuid();
        var first = await _repository.CreateAsync(
            Guid.NewGuid(), session,
            new[] { new OfflineAuthorityBudgetLine("bills.comp", 100m, 1) }, _now, _now.AddHours(4));

        var second = await _repository.CreateAsync(
            Guid.NewGuid(), session,
            new[] { new OfflineAuthorityBudgetLine("bills.void", null, 3) }, _now.AddMinutes(5), _now.AddHours(5));

        second.BudgetId.Should().NotBe(first.BudgetId);
        (await _repository.GetAsync(first.BudgetId)).Should().BeNull("the prior budget and its lines are gone");

        var current = await _repository.GetBySessionAsync(session);
        current!.BudgetId.Should().Be(second.BudgetId);
        current.Lines.Should().ContainSingle(l => l.PermissionCode == "bills.void" && l.MaxCount == 3);
    }

    [Fact]
    public async Task CreateAcceptsABudgetWithNoLines()
    {
        var created = await _repository.CreateAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            Array.Empty<OfflineAuthorityBudgetLine>(), _now, _now.AddHours(4));

        (await _repository.GetAsync(created.BudgetId))!.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateRejectsANonPositiveWindow()
        => await FluentActions.Invoking(() => _repository.CreateAsync(
                Guid.NewGuid(), Guid.NewGuid(), Lines(), _now, _now))
            .Should().ThrowAsync<ArgumentException>();

    [Fact]
    public async Task UnknownLookupsReturnNull()
    {
        (await _repository.GetAsync(Guid.NewGuid())).Should().BeNull();
        (await _repository.GetBySessionAsync(Guid.NewGuid())).Should().BeNull();
    }
}
