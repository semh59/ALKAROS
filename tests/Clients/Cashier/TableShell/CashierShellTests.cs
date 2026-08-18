using FluentAssertions;
using Xunit;

namespace ALKAROS.Clients.Cashier.TableShell.Tests;

public sealed class CashierShellTests
{
    private readonly CashierShellEngine _engine = new();

    [Fact]
    public void ExpiredSessionInvalidatesStateAndRedirectsToLogin()
    {
        var expiredSession = new CashierSession(
            SessionId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            UserName: "Kasiyer Ahmet",
            TerminalId: "POS-01",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-5), // Expired 5 mins ago
            IsActive: true);

        _engine.SetSession(expiredSession);

        _engine.IsSessionExpired().Should().BeTrue();

        var selected = _engine.SelectTable(Guid.NewGuid());
        selected.Should().BeFalse();

        var state = _engine.CurrentState;
        state.IsSessionExpired.Should().BeTrue();
        state.ErrorMessage.Should().Contain("Oturum süresi doldu");
    }

    [Fact]
    public void TableSectionFilteringDisplaysCorrectSubset()
    {
        var tables = new List<TableCardViewModel>
        {
            new(Guid.NewGuid(), "S-01", "Salon", TableViewStatus.Available, 4, null, 1, null),
            new(Guid.NewGuid(), "S-02", "Salon", TableViewStatus.Occupied, 2, 350.00m, 1, DateTimeOffset.UtcNow.AddMinutes(-30)),
            new(Guid.NewGuid(), "B-01", "Bahçe", TableViewStatus.Available, 6, null, 1, null),
            new(Guid.NewGuid(), "T-01", "Teras", TableViewStatus.Reserved, 4, null, 1, null)
        };

        _engine.LoadTables(tables);

        // 1. All sections
        _engine.SetSectionFilter(CashierShellEngine.AllSections);
        _engine.GetFilteredTables().Should().HaveCount(4);

        // 2. Salon section
        _engine.SetSectionFilter("Salon");
        var salonTables = _engine.GetFilteredTables();
        salonTables.Should().HaveCount(2);
        salonTables.Should().OnlyContain(t => t.Section == "Salon");

        // 3. Bahçe section
        _engine.SetSectionFilter("Bahçe");
        var bahceTables = _engine.GetFilteredTables();
        bahceTables.Should().HaveCount(1);
        bahceTables[0].TableNumber.Should().Be("B-01");
    }

    [Fact]
    public void RowVersionMismatchSurfacesClearConcurrencyErrorMessage()
    {
        var session = new CashierSession(
            SessionId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            UserName: "Kasiyer Ayşe",
            TerminalId: "POS-02",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(4),
            IsActive: true);

        _engine.SetSession(session);

        var tableId = Guid.NewGuid();
        var initialTable = new TableCardViewModel(tableId, "S-05", "Salon", TableViewStatus.Available, 4, null, 1, null);
        _engine.LoadTables(new[] { initialTable });

        // Server has row_version 2 now, but client tried to send update with expected version 1
        var serverUpdatedTable = initialTable with { RowVersion = 2, Status = TableViewStatus.Occupied, ActiveBillAmount = 180.00m };

        var success = _engine.ApplyTableUpdate(serverUpdatedTable, clientExpectedVersion: 1);

        success.Should().BeFalse();
        _engine.CurrentState.ErrorMessage.Should().Contain("başka bir terminal tarafından güncellendi");
    }

    [Fact]
    public void TableSelectionAndStateOperatesAccurately()
    {
        var session = new CashierSession(
            SessionId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            UserName: "Kasiyer Mehmet",
            TerminalId: "POS-01",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(2),
            IsActive: true);

        _engine.SetSession(session);

        var table1 = new TableCardViewModel(Guid.NewGuid(), "M-01", "Salon", TableViewStatus.Available, 4, null, 1, null);
        var table2 = new TableCardViewModel(Guid.NewGuid(), "M-02", "Salon", TableViewStatus.Occupied, 2, 120.00m, 1, DateTimeOffset.UtcNow);
        _engine.LoadTables(new[] { table1, table2 });

        var selectResult = _engine.SelectTable(table2.TableId);
        selectResult.Should().BeTrue();

        var state = _engine.CurrentState;
        state.SelectedTable.Should().NotBeNull();
        state.SelectedTable!.TableNumber.Should().Be("M-02");
        state.SelectedTable.ActiveBillAmount.Should().Be(120.00m);
    }

    [Fact]
    public void TwoDimensionalTableStateMaintainsOccupancyAndOperationalBadgeDistinctly()
    {
        var session = new CashierSession(
            SessionId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            UserName: "Kasiyer Mehmet",
            TerminalId: "POS-01",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(2),
            IsActive: true);

        _engine.SetSession(session);

        var table = new TableCardViewModel(
            TableId: Guid.NewGuid(),
            TableNumber: "S-02",
            Section: "Salon",
            Status: TableViewStatus.Occupied,
            Capacity: 4,
            ActiveBillAmount: 485.00m,
            RowVersion: 1,
            OccupiedSince: DateTimeOffset.UtcNow.AddMinutes(-35),
            IsSelected: false,
            OperationalBadge: TableOperationalBadge.BillRequested);

        _engine.LoadTables(new[] { table });

        var tables = _engine.GetFilteredTables();
        tables.Should().HaveCount(1);
        tables[0].Status.Should().Be(TableViewStatus.Occupied);
        tables[0].OperationalBadge.Should().Be(TableOperationalBadge.BillRequested);
    }
}
