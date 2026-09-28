namespace ALKAROS.CustomerData.AnonymizationState.Tests;

using ALKAROS.CustomerData.AnonymizationState.Tests.Fixtures;
using Xunit;

public sealed class PostgresCustomerAnonymizationRequestStoreTests : IAsyncLifetime
{
    private readonly CustomerAnonymizationTestDatabase _database = new();
    private PostgresCustomerAnonymizationRequestStore _store = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _store = new PostgresCustomerAnonymizationRequestStore(_database.DataSource);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task GetOnAnUnknownRequestReturnsNull()
    {
        Assert.Null(await _store.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreatingAPendingRequestRoundTrips()
    {
        var customerId = Guid.NewGuid();
        var requestedAt = DateTimeOffset.UtcNow;

        var id = await _store.CreateAsync(customerId, AnonymizationRequestStatus.Pending, "manager-1", null, requestedAt);
        var request = await _store.GetAsync(id);

        Assert.NotNull(request);
        Assert.Equal(customerId, request!.CustomerId);
        Assert.Equal(AnonymizationRequestStatus.Pending, request.Status);
        Assert.Equal("manager-1", request.RequestedBy);
        Assert.Null(request.BlockedReason);
        Assert.Null(request.AnonymizedAt);
        Assert.Equal(1, request.RowVersion);
    }

    [Fact]
    public async Task CreatingARetentionBlockedRequestRoundTrips()
    {
        var id = await _store.CreateAsync(Guid.NewGuid(), AnonymizationRequestStatus.RetentionBlocked, null, "open invoice", DateTimeOffset.UtcNow);

        var request = await _store.GetAsync(id);

        Assert.Equal(AnonymizationRequestStatus.RetentionBlocked, request!.Status);
        Assert.Equal("open invoice", request.BlockedReason);
    }

    [Fact]
    public async Task UpdateStatusToPendingClearsTheBlockedReasonAndBumpsRowVersion()
    {
        var id = await _store.CreateAsync(Guid.NewGuid(), AnonymizationRequestStatus.RetentionBlocked, null, "open invoice", DateTimeOffset.UtcNow);

        await _store.UpdateStatusAsync(id, AnonymizationRequestStatus.Pending, blockedReason: null, anonymizedAt: null, expectedRowVersion: 1);

        var request = await _store.GetAsync(id);
        Assert.Equal(AnonymizationRequestStatus.Pending, request!.Status);
        Assert.Null(request.BlockedReason);
        Assert.Equal(2, request.RowVersion);
    }

    [Fact]
    public async Task UpdateStatusToAnonymizedSetsAnonymizedAt()
    {
        var id = await _store.CreateAsync(Guid.NewGuid(), AnonymizationRequestStatus.Pending, null, null, DateTimeOffset.UtcNow);
        var anonymizedAt = DateTimeOffset.UtcNow;

        await _store.UpdateStatusAsync(id, AnonymizationRequestStatus.Anonymized, blockedReason: null, anonymizedAt, expectedRowVersion: 1);

        var request = await _store.GetAsync(id);
        Assert.Equal(AnonymizationRequestStatus.Anonymized, request!.Status);
        Assert.Equal(anonymizedAt, request.AnonymizedAt);
    }

    [Fact]
    public async Task UpdateStatusWithAStaleRowVersionThrowsConcurrencyException()
    {
        var id = await _store.CreateAsync(Guid.NewGuid(), AnonymizationRequestStatus.Pending, null, null, DateTimeOffset.UtcNow);
        await _store.UpdateStatusAsync(id, AnonymizationRequestStatus.Anonymized, null, DateTimeOffset.UtcNow, expectedRowVersion: 1);

        await Assert.ThrowsAsync<CustomerAnonymizationConcurrencyException>(() =>
            _store.UpdateStatusAsync(id, AnonymizationRequestStatus.Anonymized, null, DateTimeOffset.UtcNow, expectedRowVersion: 1));
    }

    [Fact]
    public async Task UpdateStatusOnAnUnknownRequestThrowsNotFound()
    {
        await Assert.ThrowsAsync<CustomerAnonymizationRequestNotFoundException>(() =>
            _store.UpdateStatusAsync(Guid.NewGuid(), AnonymizationRequestStatus.Pending, null, null, expectedRowVersion: 1));
    }
}
