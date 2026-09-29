using ALKAROS.CustomerAccounts.AccountPayments.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.CustomerAccounts.AccountPayments.Tests;

/// <summary>V14-ACC-004 persistence against real PostgreSQL: idempotency, evidence uniqueness, history, concurrency.</summary>
public sealed class PostgresAccountPaymentRepositoryTests : IClassFixture<AccountPaymentTestDatabase>
{
    private readonly AccountPaymentTestDatabase _database;
    private readonly PostgresAccountPaymentRepository _repository;

    public PostgresAccountPaymentRepositoryTests(AccountPaymentTestDatabase database)
    {
        _database = database;
        _repository = new PostgresAccountPaymentRepository(database.DataSource);
    }

    private static AccountPayment NewCash(Guid customerId, decimal amount = 100m, string? key = null) =>
        AccountPayment.Request(customerId, AccountPaymentMethod.Cash, amount, key ?? Guid.NewGuid().ToString(), null, DateTimeOffset.UtcNow);

    [Fact]
    public async Task TheSameIdempotencyKeyProducesOneAccountPayment()
    {
        var customerId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();

        var first = await _repository.RequestAsync(NewCash(customerId, key: key));
        var retry = await _repository.RequestAsync(NewCash(customerId, key: key));

        Assert.Equal(first.Id, retry.Id);
        Assert.Single(await _repository.GetByCustomerAsync(customerId));
    }

    [Fact]
    public async Task ConcurrentRequestsWithOneKeyProduceOneAccountPayment()
    {
        var customerId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _repository.RequestAsync(NewCash(customerId, key: key))));

        Assert.Single(results.Select(r => r.Id).Distinct());
        Assert.Single(await _repository.GetByCustomerAsync(customerId));
    }

    [Fact]
    public async Task AKeyReusedForADifferentPaymentIsRejected()
    {
        var key = Guid.NewGuid().ToString();
        await _repository.RequestAsync(NewCash(Guid.NewGuid(), key: key));

        await Assert.ThrowsAsync<AccountPaymentIdempotencyKeyReusedException>(
            () => _repository.RequestAsync(NewCash(Guid.NewGuid(), key: key)));
    }

    [Fact]
    public async Task ApprovalStoresTheEvidenceAndAppendsHistory()
    {
        var requested = await _repository.RequestAsync(NewCash(Guid.NewGuid()));
        var evidence = AccountPaymentEvidence.ForCashTransaction(Guid.NewGuid());
        var changedBy = Guid.NewGuid();

        var approved = await _repository.SaveTransitionAsync(
            requested.Approve(evidence, DateTimeOffset.UtcNow), requested.RowVersion, "Kasadan tahsil edildi", changedBy);

        var stored = await _repository.GetAsync(requested.Id);
        Assert.Equal(AccountPaymentStatus.Approved, stored!.Status);
        Assert.Equal(evidence, stored.Evidence);
        Assert.Equal(2, stored.RowVersion);
        Assert.Equal(approved.RowVersion, stored.RowVersion);

        var history = await _repository.GetHistoryAsync(requested.Id);
        Assert.Collection(history,
            first => { Assert.Null(first.OldStatus); Assert.Equal(AccountPaymentStatus.Requested, first.NewStatus); },
            second =>
            {
                Assert.Equal(AccountPaymentStatus.Requested, second.OldStatus);
                Assert.Equal(AccountPaymentStatus.Approved, second.NewStatus);
                Assert.Equal(evidence.Reference, second.EvidenceReference);
                Assert.Equal(changedBy, second.ChangedBy);
            });
    }

    [Fact]
    public async Task OneCashTransactionCannotProveTwoAccountPayments()
    {
        var evidence = AccountPaymentEvidence.ForCashTransaction(Guid.NewGuid());
        var first = await _repository.RequestAsync(NewCash(Guid.NewGuid()));
        var second = await _repository.RequestAsync(NewCash(Guid.NewGuid()));
        await _repository.SaveTransitionAsync(first.Approve(evidence, DateTimeOffset.UtcNow), first.RowVersion, null, null);

        await Assert.ThrowsAsync<AccountPaymentEvidenceAlreadyLinkedException>(
            () => _repository.SaveTransitionAsync(second.Approve(evidence, DateTimeOffset.UtcNow), second.RowVersion, null, null));

        Assert.Equal(AccountPaymentStatus.Requested, (await _repository.GetAsync(second.Id))!.Status);
    }

    [Fact]
    public async Task AStaleRowVersionIsRejected()
    {
        var requested = await _repository.RequestAsync(NewCash(Guid.NewGuid()));
        await _repository.SaveTransitionAsync(requested.MarkUnknown(DateTimeOffset.UtcNow), requested.RowVersion, null, null);

        await Assert.ThrowsAsync<AccountPaymentConcurrencyException>(
            () => _repository.SaveTransitionAsync(requested.Decline(DateTimeOffset.UtcNow), requested.RowVersion, null, null));
    }

    [Fact]
    public async Task ATerminalPaymentCannotMoveAgainEvenFromAFreshRead()
    {
        var requested = await _repository.RequestAsync(NewCash(Guid.NewGuid()));
        var declined = await _repository.SaveTransitionAsync(requested.Decline(DateTimeOffset.UtcNow), requested.RowVersion, null, null);
        var forged = new AccountPayment(declined.Id, declined.CustomerId, declined.Method, declined.Amount, declined.IdempotencyKey,
            declined.RequestedAt, status: AccountPaymentStatus.Unknown, rowVersion: declined.RowVersion);

        await Assert.ThrowsAsync<InvalidAccountPaymentTransitionException>(
            () => _repository.SaveTransitionAsync(forged, declined.RowVersion, null, null));
    }

    [Fact]
    public async Task TheDatabaseRejectsAnApprovedRowWithoutEvidence()
    {
        var requested = await _repository.RequestAsync(NewCash(Guid.NewGuid()));

        await using var command = _database.DataSource.CreateCommand(
            "UPDATE customer_account.account_payments SET status = 'Approved' WHERE account_payment_id = @id;");
        command.Parameters.AddWithValue("id", requested.Id);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task StatusHistoryIsAppendOnly()
    {
        var requested = await _repository.RequestAsync(NewCash(Guid.NewGuid()));

        await using var command = _database.DataSource.CreateCommand(
            "DELETE FROM customer_account.account_payment_status_history WHERE account_payment_id = @id;");
        command.Parameters.AddWithValue("id", requested.Id);
        await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task ATransitionInsideACallersTransactionRollsBackWithIt()
    {
        var requested = await _repository.RequestAsync(NewCash(Guid.NewGuid()));

        await using (var connection = await _database.DataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await _repository.SaveTransitionAsync(requested.MarkUnknown(DateTimeOffset.UtcNow), requested.RowVersion, null, null, connection, transaction);
            await transaction.RollbackAsync();
        }

        Assert.Equal(AccountPaymentStatus.Requested, (await _repository.GetAsync(requested.Id))!.Status);
        Assert.Single(await _repository.GetHistoryAsync(requested.Id));
    }
}
