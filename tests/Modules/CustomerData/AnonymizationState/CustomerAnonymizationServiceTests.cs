namespace ALKAROS.CustomerData.AnonymizationState.Tests;

using System.Security.Cryptography;
using ALKAROS.Audit.EventStore;
using ALKAROS.CustomerData.AnonymizationState.Tests.Fixtures;
using ALKAROS.CustomerData.Profiles;
using ALKAROS.Secrets;
using Xunit;

/// <summary>Real Postgres end-to-end: real ICustomerProfileStore, real ICustomerAnonymizationRequestStore, real IAuditEventStore. Only the retention guard is a controllable test double, since neither real blocking-reference owner (V14-ACC/V14-INV) exists yet.</summary>
public sealed class CustomerAnonymizationServiceTests : IAsyncLifetime
{
    private sealed class FixedGuard(string? reason) : IAnonymizationRetentionGuard
    {
        public Task<string?> CheckAsync(Guid customerId, CancellationToken cancellationToken) => Task.FromResult(reason);
    }

    private readonly CustomerAnonymizationTestDatabase _database = new();
    private PostgresCustomerProfileStore _profiles = null!;
    private PostgresCustomerAnonymizationRequestStore _requests = null!;
    private PostgresAuditEventStore _audit = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var secretProvider = new InMemorySecretProvider();
        secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        _profiles = new PostgresCustomerProfileStore(_database.DataSource, secretProvider);
        _requests = new PostgresCustomerAnonymizationRequestStore(_database.DataSource);
        _audit = new PostgresAuditEventStore(_database.DataSource);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private CustomerAnonymizationService Service(string? blockingReason = null) =>
        new(_requests, _profiles, new FixedGuard(blockingReason), _audit);

    [Fact]
    public async Task ARequestWithNoBlockerGoesStraightToPendingAndIsAudited()
    {
        var customerId = await _profiles.CreateAsync(new CreateCustomerProfileRequest("Ayşe", "0555", "a@x.com", "İstanbul"));

        var request = await Service(blockingReason: null).RequestAsync(customerId, "manager-1");

        Assert.Equal(AnonymizationRequestStatus.Pending, request.Status);
        Assert.Null(request.BlockedReason);

        var events = await _audit.GetByAggregateAsync("CustomerAnonymizationRequest", request.Id);
        Assert.Contains(events, e => e.EventName == "CustomerAnonymizationRequested");
    }

    [Fact]
    public async Task ARequestWithABlockerGoesToRetentionBlockedAndRecordsTheReason()
    {
        var customerId = await _profiles.CreateAsync(new CreateCustomerProfileRequest("Ayşe", null, null, null));

        var request = await Service(blockingReason: "open invoice").RequestAsync(customerId, null);

        Assert.Equal(AnonymizationRequestStatus.RetentionBlocked, request.Status);
        Assert.Equal("open invoice", request.BlockedReason);

        var events = await _audit.GetByAggregateAsync("CustomerAnonymizationRequest", request.Id);
        var blocked = Assert.Single(events, e => e.EventName == "CustomerAnonymizationBlocked");
        Assert.Equal("open invoice", blocked.Reason);
    }

    [Fact]
    public async Task ReevaluatingAStillBlockedRequestChangesNothing()
    {
        var customerId = await _profiles.CreateAsync(new CreateCustomerProfileRequest(null, null, null, null));
        var request = await Service(blockingReason: "open invoice").RequestAsync(customerId, null);

        var reevaluated = await Service(blockingReason: "open invoice").ReevaluateAsync(request.Id);

        Assert.Equal(AnonymizationRequestStatus.RetentionBlocked, reevaluated.Status);
        Assert.Equal(request.RowVersion, reevaluated.RowVersion);
    }

    [Fact]
    public async Task ReevaluatingAClearedBlockerMovesToPending()
    {
        var customerId = await _profiles.CreateAsync(new CreateCustomerProfileRequest(null, null, null, null));
        var request = await Service(blockingReason: "open invoice").RequestAsync(customerId, null);

        var reevaluated = await Service(blockingReason: null).ReevaluateAsync(request.Id);

        Assert.Equal(AnonymizationRequestStatus.Pending, reevaluated.Status);
        Assert.Null(reevaluated.BlockedReason);
    }

    [Fact]
    public async Task ExecutingAPendingRequestActuallyAnonymizesTheProfileAndOnlyTheConfiguredFields()
    {
        var customerId = await _profiles.CreateAsync(new CreateCustomerProfileRequest("Ayşe Yılmaz", "0555", "a@x.com", "İstanbul"));
        var request = await Service(blockingReason: null).RequestAsync(customerId, null);

        var executed = await Service().ExecuteAsync(request.Id, request.RowVersion);

        Assert.Equal(AnonymizationRequestStatus.Anonymized, executed.Status);
        Assert.NotNull(executed.AnonymizedAt);

        var profile = await _profiles.GetAsync(customerId, CustomerAccessRole.Manager);
        Assert.True(profile!.Anonymized);
        Assert.Null(profile.Name);
        Assert.Null(profile.Phone);
        Assert.Null(profile.Email);
        Assert.Null(profile.Address);
        // The record and its id survive - referential integrity for order/
        // invoice history still needs to point somewhere.
        Assert.Equal(customerId, profile.CustomerId);

        var events = await _audit.GetByAggregateAsync("CustomerAnonymizationRequest", request.Id);
        Assert.Contains(events, e => e.EventName == "CustomerAnonymized");
    }

    [Fact]
    public async Task ExecutingAnAlreadyAnonymizedRequestIsIdempotent()
    {
        var customerId = await _profiles.CreateAsync(new CreateCustomerProfileRequest("Ayşe", null, null, null));
        var request = await Service(blockingReason: null).RequestAsync(customerId, null);
        var executed = await Service().ExecuteAsync(request.Id, request.RowVersion);

        var exception = await Record.ExceptionAsync(() => Service().ExecuteAsync(request.Id, executed.RowVersion));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ExecutingAStillRetentionBlockedRequestIsRejected()
    {
        var customerId = await _profiles.CreateAsync(new CreateCustomerProfileRequest(null, null, null, null));
        var request = await Service(blockingReason: "open invoice").RequestAsync(customerId, null);

        await Assert.ThrowsAsync<CustomerAnonymizationInvalidTransitionException>(() =>
            Service().ExecuteAsync(request.Id, request.RowVersion));
    }

    [Fact]
    public async Task ReevaluatingAnUnknownRequestThrowsNotFound()
    {
        await Assert.ThrowsAsync<CustomerAnonymizationRequestNotFoundException>(() =>
            Service().ReevaluateAsync(Guid.NewGuid()));
    }
}
