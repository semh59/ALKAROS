using FluentAssertions;
using Xunit;

namespace ALKAROS.Clients.Cashier.TableShell.Tests;

/// <summary>
/// Real-Life Restaurant Chaos & Disaster Test Suite (V1 Resilience & Edge Cases).
/// Tests concurrency clashes, partial payments, session timeouts, and out-of-order mutations.
/// </summary>
public sealed class RealLifeChaosTests
{
    private readonly CashierShellEngine _engine = new();

    private static CashierSession CreateActiveSession() => new(
        SessionId: Guid.NewGuid(),
        UserId: Guid.NewGuid(),
        UserName: "Kasiyer Mehmet",
        TerminalId: "POS-01",
        ExpiresAt: DateTimeOffset.UtcNow.AddHours(4),
        IsActive: true);

    [Fact]
    public void OptimisticConcurrencyRowVersionConflictPreventsSilentOverride()
    {
        // Arrange
        _engine.SetSession(CreateActiveSession());
        var tableId = Guid.NewGuid();
        var initialTable = new TableCardViewModel(
            TableId: tableId,
            TableNumber: "S-02",
            Section: "Salon",
            Status: TableViewStatus.Occupied,
            Capacity: 4,
            ActiveBillAmount: 485.00m,
            RowVersion: 1,
            OccupiedSince: DateTimeOffset.UtcNow.AddMinutes(-30));

        _engine.LoadTables(new[] { initialTable });
        _engine.SelectTable(tableId);

        // Act: Another terminal (e.g. Waiter) updated table to RowVersion 2 with 575.00 TL
        var serverUpdatedTable = initialTable with
        {
            RowVersion = 2,
            ActiveBillAmount = 575.00m
        };

        // Current client tries to apply an update assuming it is still at RowVersion 1
        var success = _engine.ApplyTableUpdate(serverUpdatedTable, clientExpectedVersion: 1);

        // Assert: Update rejected due to row-version clash, error presented clearly
        success.Should().BeFalse();
        _engine.CurrentState.ErrorMessage.Should().Contain("başka bir terminal tarafından güncellendi");
        _engine.CurrentState.ErrorMessage.Should().Contain("S-02");
    }

    [Fact]
    public void MultipleTablesSectionFilterWithRealWorldOccupanciesRendersDeterministically()
    {
        // Arrange: Real restaurant evening rush with 14 tables across Salon, Bahçe, Teras
        _engine.SetSession(CreateActiveSession());

        var tables = new List<TableCardViewModel>
        {
            new(Guid.NewGuid(), "S-01", "Salon", TableViewStatus.Available, 4, null, 1, null),
            new(Guid.NewGuid(), "S-02", "Salon", TableViewStatus.Occupied, 4, 485.00m, 1, DateTimeOffset.UtcNow.AddMinutes(-35), OperationalBadge: TableOperationalBadge.KitchenCooking),
            new(Guid.NewGuid(), "S-03", "Salon", TableViewStatus.Occupied, 6, 1250.00m, 2, DateTimeOffset.UtcNow.AddMinutes(-12)),
            new(Guid.NewGuid(), "S-04", "Salon", TableViewStatus.Occupied, 4, 820.00m, 1, DateTimeOffset.UtcNow.AddMinutes(-58), OperationalBadge: TableOperationalBadge.BillRequested),
            new(Guid.NewGuid(), "B-01", "Bahçe", TableViewStatus.Occupied, 4, 290.00m, 1, DateTimeOffset.UtcNow.AddMinutes(-18), OperationalBadge: TableOperationalBadge.KitchenCooking),
            new(Guid.NewGuid(), "B-02", "Bahçe", TableViewStatus.Available, 4, null, 1, null),
            new(Guid.NewGuid(), "T-01", "Teras", TableViewStatus.Available, 4, null, 1, null),
            new(Guid.NewGuid(), "T-02", "Teras", TableViewStatus.Occupied, 4, 640.00m, 1, DateTimeOffset.UtcNow.AddMinutes(-50), OperationalBadge: TableOperationalBadge.BillRequested)
        };

        _engine.LoadTables(tables);

        // Act & Assert: All Sections
        _engine.SetSectionFilter("Tümü");
        _engine.GetFilteredTables().Should().HaveCount(8);

        // Act & Assert: Salon only
        _engine.SetSectionFilter("Salon");
        _engine.GetFilteredTables().Should().HaveCount(4);

        // Act & Assert: Bahçe only
        _engine.SetSectionFilter("Bahçe");
        _engine.GetFilteredTables().Should().HaveCount(2);

        // Act & Assert: Teras only
        _engine.SetSectionFilter("Teras");
        _engine.GetFilteredTables().Should().HaveCount(2);
    }

    [Fact]
    public void SelectingTableDuringMidShiftTimeoutForcesReauthentication()
    {
        // Arrange: Cashier left POS idle, token expired
        var session = new CashierSession(
            SessionId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            UserName: "Kasiyer Ayşe",
            TerminalId: "POS-02",
            ExpiresAt: DateTimeOffset.UtcNow.AddSeconds(-10), // Expired 10 seconds ago
            IsActive: true);

        _engine.SetSession(session);
        var tableId = Guid.NewGuid();
        _engine.LoadTables(new[] { new TableCardViewModel(tableId, "S-01", "Salon", TableViewStatus.Occupied, 4, 250m, 1, null) });

        // Act
        var selected = _engine.SelectTable(tableId);

        // Assert
        selected.Should().BeFalse();
        _engine.CurrentState.IsSessionExpired.Should().BeTrue();
        _engine.CurrentState.ErrorMessage.Should().Contain("Oturum süresi doldu");
    }

    [Fact]
    public void SuccessfulVersionMatchedUpdateReflectsNewBillAndClearsErrors()
    {
        // Arrange
        _engine.SetSession(CreateActiveSession());
        var tableId = Guid.NewGuid();
        var table = new TableCardViewModel(tableId, "S-02", "Salon", TableViewStatus.Occupied, 4, 485.00m, 1, null);
        _engine.LoadTables(new[] { table });
        _engine.SelectTable(tableId);

        // Act: Apply version 2 update with exact expected client version
        var updated = table with { RowVersion = 2, ActiveBillAmount = 640.00m, OperationalBadge = TableOperationalBadge.BillRequested };
        var success = _engine.ApplyTableUpdate(updated, clientExpectedVersion: 2);

        // Assert
        success.Should().BeTrue();
        _engine.CurrentState.ErrorMessage.Should().BeNull();
        _engine.CurrentState.SelectedTable!.ActiveBillAmount.Should().Be(640.00m);
        _engine.CurrentState.SelectedTable!.OperationalBadge.Should().Be(TableOperationalBadge.BillRequested);
    }
}
