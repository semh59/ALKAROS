namespace ALKAROS.CustomerAccounts.BalanceProjection.Tests;

using ALKAROS.CustomerAccounts.Aging;
using ALKAROS.CustomerAccounts.BalanceProjection.Tests.Fixtures;
using ALKAROS.CustomerAccounts.TransactionLedger;
using Xunit;

[CollectionDefinition("Account balance PostgreSQL")]
public sealed class AccountBalancePostgresqlDefinition;

[Collection("Account balance PostgreSQL")]
public sealed class PostgresReceivablesAgingServiceTests : IAsyncLifetime
{
    private static readonly BusinessCalendar Istanbul = new("Europe/Istanbul", 6);
    private static readonly DateOnly AsOf = new(2026, 11, 7);

    private readonly AccountBalanceTestDatabase _database = new();
    private PostgresReceivablesAgingService _service = null!;
    private PostgresAccountTransactionLedger _ledger = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _service = new PostgresReceivablesAgingService(_database.DataSource);
        _ledger = new PostgresAccountTransactionLedger(_database.DataSource);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    // Istanbul is UTC+3 all year: 12:00 local is 09:00Z.
    private static DateTimeOffset Local(int ageDays, int hour = 12, int minute = 0)
        => new(AsOf.AddDays(-ageDays).ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(3));

    private async Task Record(Guid customerId, AccountTransactionType type, decimal amount, int ageDays, int hour = 12, int minute = 0)
        => await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, type, amount, "Test", Guid.NewGuid(), type == AccountTransactionType.Adjustment ? "Düzeltme" : null,
            Guid.NewGuid(), Local(ageDays, hour, minute).ToUniversalTime()));

    private Task Charge(Guid customerId, decimal amount, int ageDays, int hour = 12, int minute = 0)
        => Record(customerId, AccountTransactionType.Charge, amount, ageDays, hour, minute);

    private Task Pay(Guid customerId, decimal amount, int ageDays)
        => Record(customerId, AccountTransactionType.Payment, amount, ageDays);

    private async Task<CustomerAging> AgeAsync(Guid customerId)
        => Assert.Single(await _service.GetAsync(AsOf, Istanbul), row => row.CustomerId == customerId);

    private async Task SetTermAsync(Guid customerId, int? days)
    {
        await using var command = _database.DataSource.CreateCommand(
            """
            INSERT INTO customer_account.credit_terms (customer_id, credit_limit, payment_term_days, updated_at, updated_by)
            VALUES (@id, 1000, @days, now(), gen_random_uuid());
            """);
        command.Parameters.AddWithValue("id", customerId);
        command.Parameters.AddWithValue("days", (object?)days ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task TamperBalanceAsync(Guid customerId, decimal balance)
    {
        await using var tamper = _database.DataSource.CreateCommand(
            "UPDATE customer_account.balances SET current_balance = @balance WHERE customer_id = @id;");
        tamper.Parameters.AddWithValue("id", customerId);
        tamper.Parameters.AddWithValue("balance", balance);
        await tamper.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task EachCustomerIsAgedFromTheirOwnLedgerAndAgreesWithTheTriggerBalance()
    {
        var big = Guid.NewGuid();
        var small = Guid.NewGuid();
        var settled = Guid.NewGuid();
        var prepaid = Guid.NewGuid();
        await Charge(big, 300m, 95);
        await Charge(big, 100m, 45);
        await Pay(big, 120.50m, 20);
        await Record(big, AccountTransactionType.Adjustment, -10m, 15);
        await Charge(small, 40m, 3);
        await Charge(settled, 70m, 50);
        await Pay(settled, 70m, 49);
        await Pay(prepaid, 25m, 10);

        var report = await _service.GetAsync(AsOf, Istanbul);

        Assert.Equal(new[] { big, small, prepaid }, report.Select(row => row.CustomerId));
        var bigRow = report[0];
        Assert.Equal((0m, 100m, 0m, 169.50m, 0m), (bigRow.Days0To30, bigRow.Days31To60, bigRow.Days61To90, bigRow.Over90, bigRow.UnappliedCredit));
        Assert.Equal(40m, report[1].Days0To30);
        Assert.Equal(25m, report[2].UnappliedCredit);
        Assert.All(report, row => Assert.True(row.IsBalanced));
    }

    [Theory]
    [InlineData(0, 100, 0, 0, 0)]
    [InlineData(30, 100, 0, 0, 0)]
    [InlineData(31, 0, 100, 0, 0)]
    [InlineData(60, 0, 100, 0, 0)]
    [InlineData(61, 0, 0, 100, 0)]
    [InlineData(90, 0, 0, 100, 0)]
    [InlineData(91, 0, 0, 0, 100)]
    public async Task AChargeLandsInTheBucketOfItsAgeInDays(int ageDays, decimal first, decimal second, decimal third, decimal older)
    {
        var customer = Guid.NewGuid();
        await Charge(customer, 100m, ageDays);

        var aging = await AgeAsync(customer);

        Assert.Equal((first, second, third, older), (aging.Days0To30, aging.Days31To60, aging.Days61To90, aging.Over90));
        Assert.Equal(100m, aging.TotalDebt);
        Assert.True(aging.IsBalanced);
    }

    [Fact]
    public async Task ABusinessDayStartsAtSixSoTheEarlyMorningBelongsToThePreviousDay()
    {
        var atSix = Guid.NewGuid();
        var justBeforeSix = Guid.NewGuid();
        await Charge(atSix, 40m, 30, hour: 6);
        await Charge(justBeforeSix, 40m, 30, hour: 5, minute: 59);

        Assert.Equal(40m, (await AgeAsync(atSix)).Days0To30);
        Assert.Equal(40m, (await AgeAsync(justBeforeSix)).Days31To60);
    }

    [Fact]
    public async Task PaymentsSettleTheOldestChargeFirstAndLeaveTheRestOfAPartlyPaidOneInItsBucket()
    {
        var customer = Guid.NewGuid();
        await Charge(customer, 100.50m, 100);
        await Charge(customer, 200m, 70);
        await Charge(customer, 50m, 10);
        await Pay(customer, 150.75m, 5);

        var aging = await AgeAsync(customer);

        Assert.Equal((50m, 0m, 149.75m, 0m), (aging.Days0To30, aging.Days31To60, aging.Days61To90, aging.Over90));
        Assert.True(aging.IsBalanced);
    }

    [Fact]
    public async Task ANegativeAdjustmentReducesTheOldestDebtLikeAPayment()
    {
        var customer = Guid.NewGuid();
        await Charge(customer, 80m, 45);
        await Charge(customer, 20m, 2);
        await Record(customer, AccountTransactionType.Adjustment, -30m, 1);

        var aging = await AgeAsync(customer);

        Assert.Equal((20m, 50m), (aging.Days0To30, aging.Days31To60));
    }

    [Fact]
    public async Task AnOverpaymentIsKeptAsCreditInsteadOfAFutureNegativeBucket()
    {
        var customer = Guid.NewGuid();
        await Charge(customer, 100m, 40);
        await Pay(customer, 130m, 3);

        var aging = await AgeAsync(customer);

        Assert.Equal(0m, aging.TotalDebt);
        Assert.Equal(30m, aging.UnappliedCredit);
        Assert.True(aging.IsBalanced);
    }

    [Fact]
    public async Task CreditPaidBeforeAChargeSettlesThatChargeWhenItArrives()
    {
        var customer = Guid.NewGuid();
        await Pay(customer, 60m, 20);
        await Charge(customer, 100m, 10);

        var aging = await AgeAsync(customer);

        Assert.Equal(40m, aging.Days0To30);
        Assert.Equal(0m, aging.UnappliedCredit);
    }

    [Fact]
    public async Task LeftoverCreditKeepsSettlingEachFollowingCharge()
    {
        var customer = Guid.NewGuid();
        await Pay(customer, 100m, 20);
        await Charge(customer, 30m, 10);
        await Charge(customer, 50m, 5);
        await Charge(customer, 40m, 1);

        var aging = await AgeAsync(customer);

        Assert.Equal((20m, 0m), (aging.Days0To30, aging.UnappliedCredit));
    }

    [Fact]
    public async Task CreditLeftByAnOverpaymentSettlesTheNextChargeAndALaterPaymentReducesWhatIsLeft()
    {
        var customer = Guid.NewGuid();
        await Charge(customer, 100m, 80);
        await Pay(customer, 150m, 60);
        await Charge(customer, 70m, 20);
        await Pay(customer, 10m, 5);

        var aging = await AgeAsync(customer);

        Assert.Equal((10m, 0m, 0m, 0m, 0m), (aging.Days0To30, aging.Days31To60, aging.Days61To90, aging.Over90, aging.UnappliedCredit));
    }

    [Fact]
    public async Task ACustomerWhoseLedgerNetsToZeroButWhoseCachedBalanceDoesNotIsStillReported()
    {
        var customer = Guid.NewGuid();
        await Charge(customer, 100m, 5);
        await Pay(customer, 100m, 4);
        await TamperBalanceAsync(customer, 5m);

        Assert.False((await AgeAsync(customer)).IsBalanced);
    }

    [Fact]
    public async Task ACustomerWhoseCachedBalanceWasTamperedWithIsReportedAsNotBalanced()
    {
        var customer = Guid.NewGuid();
        await Charge(customer, 100m, 5);
        await TamperBalanceAsync(customer, 90m);

        Assert.False((await AgeAsync(customer)).IsBalanced);
    }

    [Fact]
    public async Task TheOverdueAmountIsWhatIsOpenPastTheCustomersPaymentTerm()
    {
        var customer = Guid.NewGuid();
        await SetTermAsync(customer, 30);
        await Charge(customer, 100m, 31);
        await Charge(customer, 60m, 30);
        await Charge(customer, 40m, 5);
        await Pay(customer, 20m, 1);

        var aging = await AgeAsync(customer);

        Assert.Equal((30, 80m), (aging.PaymentTermDays, aging.OverdueAmount));
    }

    [Fact]
    public async Task ACustomerWhoseDebtIsAllWithinTheTermHasAnOverdueAmountOfZeroNotNone()
    {
        var customer = Guid.NewGuid();
        await SetTermAsync(customer, 30);
        await Charge(customer, 100m, 5);

        var aging = await AgeAsync(customer);

        Assert.Equal((30, 0m), (aging.PaymentTermDays, aging.OverdueAmount));
    }

    [Theory]
    [InlineData(1, 1, false)]
    [InlineData(1, 2, true)]
    [InlineData(365, 365, false)]
    [InlineData(365, 366, true)]
    public async Task TheOverdueBoundaryHoldsAtTheShortestAndLongestAllowedTerms(int termDays, int ageDays, bool overdue)
    {
        var customer = Guid.NewGuid();
        await SetTermAsync(customer, termDays);
        await Charge(customer, 100m, ageDays);

        Assert.Equal(overdue ? 100m : 0m, (await AgeAsync(customer)).OverdueAmount);
    }

    [Fact]
    public async Task ACustomerWithoutATermHasNoOverdueFigure()
    {
        var withoutRow = Guid.NewGuid();
        var withoutDays = Guid.NewGuid();
        await SetTermAsync(withoutDays, null);
        await Charge(withoutRow, 100m, 200);
        await Charge(withoutDays, 100m, 200);

        var first = await AgeAsync(withoutRow);
        var second = await AgeAsync(withoutDays);

        Assert.Equal((null, null, null, null), (first.PaymentTermDays, first.OverdueAmount, second.PaymentTermDays, second.OverdueAmount));
    }

    [Theory]
    [InlineData(PostgresReceivablesAgingService.MaxCustomers, true)]
    [InlineData(PostgresReceivablesAgingService.MaxCustomers + 1, false)]
    public async Task AReportOfMoreCustomersThanTheCapFailsLoudInsteadOfDroppingAny(int customers, bool allowed)
    {
        await using var insert = _database.DataSource.CreateCommand(
            """
            SET session_replication_role = replica;
            INSERT INTO customer_account.account_transactions
                (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, occurred_at)
            SELECT gen_random_uuid(), gen_random_uuid(), 'Charge', 1, 'Bulk', gen_random_uuid(), @at
            FROM generate_series(1, @customers);
            SET session_replication_role = DEFAULT;
            """);
        insert.CommandTimeout = 120;
        insert.Parameters.AddWithValue("customers", customers);
        insert.Parameters.AddWithValue("at", Local(3).ToUniversalTime());
        await insert.ExecuteNonQueryAsync();

        if (allowed)
            Assert.Equal(customers, (await _service.GetAsync(AsOf, Istanbul)).Count);
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetAsync(AsOf, Istanbul));
    }

    [Fact]
    public async Task ALedgerOfMoreThanHalfAMillionRowsIsAgedInsteadOfFailing()
    {
        var customer = Guid.NewGuid();
        await using var insert = _database.DataSource.CreateCommand(
            """
            SET session_replication_role = replica;
            INSERT INTO customer_account.account_transactions
                (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, occurred_at)
            SELECT gen_random_uuid(), @id, 'Charge', 0.01, 'Bulk', gen_random_uuid(), @at
            FROM generate_series(1, 500001);
            SET session_replication_role = DEFAULT;
            """);
        insert.CommandTimeout = 120;
        insert.Parameters.AddWithValue("id", customer);
        insert.Parameters.AddWithValue("at", Local(3).ToUniversalTime());
        await insert.ExecuteNonQueryAsync();

        var aging = await AgeAsync(customer);

        Assert.Equal(5000.01m, aging.Days0To30);
    }
}
