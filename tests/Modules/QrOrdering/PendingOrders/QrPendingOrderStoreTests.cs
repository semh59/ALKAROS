namespace ALKAROS.QrOrdering.PendingOrders.Tests;

using ALKAROS.IntegrationContracts;
using ALKAROS.QrOrdering.CustomerSession;
using ALKAROS.QrOrdering.PendingOrders.Tests.Fixtures;
using ALKAROS.QrOrdering.TokenLifecycle;
using FluentAssertions;
using NpgsqlTypes;
using Xunit;

/// <summary>
/// V12-QRO-001, against a real Postgres database: table_mgmt + catalog +
/// qr_ordering (table_tokens, customer_sessions, pending_order_submissions)
/// + the real transactional outbox.
/// </summary>
public sealed class QrPendingOrderStoreTests : IClassFixture<QrOrderingPendingOrdersTestDatabase>
{
    private readonly QrOrderingPendingOrdersTestDatabase _database;
    private readonly TableTokenService _tableTokenService;
    private readonly CustomerSessionService _customerSessionService;
    private readonly QrPendingOrderStore _store;

    public QrPendingOrderStoreTests(QrOrderingPendingOrdersTestDatabase database)
    {
        _database = database;
        _tableTokenService = new TableTokenService(new PostgresTableTokenRepository(database.DataSource));
        _customerSessionService = new CustomerSessionService(
            new PostgresCustomerSessionRepository(database.DataSource), _tableTokenService);
        _store = new QrPendingOrderStore(database.DataSource, _customerSessionService);
    }

    private async Task<string> IssueSessionAsync(Guid tableId)
    {
        var rawTableToken = await _tableTokenService.IssueAsync(tableId);
        var issued = await _customerSessionService.IssueAsync(rawTableToken);
        return issued.RawToken!;
    }

    [Fact]
    public async Task SubmittingWithAValidSessionQueuesTheEventAndRecordsTheSubmission()
    {
        var tableId = await _database.SeedTableAsync();
        var productId = await _database.SeedProductAsync(price: 120m);
        var rawSession = await IssueSessionAsync(tableId);
        var submissionId = Guid.NewGuid();

        var result = await _store.SubmitAsync(
            rawSession,
            new QrOrderSubmissionRequest(
                [new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 2, "az acılı")],
                submissionId));

        result.SubmissionId.Should().Be(submissionId);
        result.TableId.Should().Be(tableId);

        var envelope = await ReadOutboxEnvelope(submissionId);
        envelope.Should().NotBeNull();
        var deserialized = IntegrationEventSerializer.Deserialize<QrOrderSubmitted>(envelope!);
        deserialized.TableId.Should().Be(tableId);
        deserialized.SubmissionId.Should().Be(submissionId);
        var item = deserialized.Items.Single();
        item.ProductId.Should().Be(productId);
        item.ProductName.Should().Be("Lahmacun");
        item.UnitPrice.Should().Be(120m);
        item.Quantity.Should().Be(2);
        item.Notes.Should().Be("az acılı");
    }

    [Fact]
    public async Task ReplayingTheSameSubmissionReturnsTheSameResultWithoutQueuingATwin()
    {
        var tableId = await _database.SeedTableAsync();
        var productId = await _database.SeedProductAsync();
        var rawSession = await IssueSessionAsync(tableId);
        var request = new QrOrderSubmissionRequest(
            [new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 1, null)],
            Guid.NewGuid());

        var first = await _store.SubmitAsync(rawSession, request);
        var second = await _store.SubmitAsync(rawSession, request);

        // The replay's SubmittedAt is re-read from Postgres (microsecond
        // precision) while the first result carries the original in-memory
        // DateTimeOffset (100ns precision) — compare with a tolerance
        // instead of exact equality.
        second.SubmissionId.Should().Be(first.SubmissionId);
        second.TableId.Should().Be(first.TableId);
        second.SubmittedAt.Should().BeCloseTo(first.SubmittedAt, TimeSpan.FromMilliseconds(1));
        (await CountOutboxMessages(request.SubmissionId)).Should().Be(1);
    }

    [Fact]
    public async Task AnUnknownSessionTokenFails()
    {
        var productId = await _database.SeedProductAsync();
        var act = () => _store.SubmitAsync(
            "alkaros-customer-session:this-was-never-issued",
            new QrOrderSubmissionRequest([new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 1, null)], Guid.NewGuid()));

        var ex = await act.Should().ThrowAsync<QrCustomerSessionInvalidException>();
        ex.Which.Reason.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task AnUnknownProductFails()
    {
        var tableId = await _database.SeedTableAsync();
        var rawSession = await IssueSessionAsync(tableId);

        var act = () => _store.SubmitAsync(
            rawSession,
            new QrOrderSubmissionRequest([new QrOrderSubmissionItemRequest(Guid.NewGuid(), Guid.NewGuid(), 1, null)], Guid.NewGuid()));

        await act.Should().ThrowAsync<QrOrderInvalidProductException>();
    }

    [Fact]
    public async Task AZeroQuantityFails()
    {
        var tableId = await _database.SeedTableAsync();
        var productId = await _database.SeedProductAsync();
        var rawSession = await IssueSessionAsync(tableId);

        var act = () => _store.SubmitAsync(
            rawSession,
            new QrOrderSubmissionRequest([new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 0, null)], Guid.NewGuid()));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnEmptyCartFails()
    {
        var tableId = await _database.SeedTableAsync();
        var rawSession = await IssueSessionAsync(tableId);

        var act = () => _store.SubmitAsync(rawSession, new QrOrderSubmissionRequest([], Guid.NewGuid()));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task FindResultingOrderReturnsNullBeforeTheOutboxIsDelivered()
    {
        var tableId = await _database.SeedTableAsync();
        var productId = await _database.SeedProductAsync();
        var rawSession = await IssueSessionAsync(tableId);
        var submissionId = Guid.NewGuid();

        await _store.SubmitAsync(
            rawSession, new QrOrderSubmissionRequest([new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, 1, null)], submissionId));

        // No Order module consumer runs in this test — QR Ordering has no
        // direct-call edge to Order (V0-ARC-001 row 19), only the queued
        // event, so the resulting Order does not exist yet from this
        // module's own point of view.
        var outcome = await _store.FindResultingOrderAsync(tableId, submissionId);
        outcome.Should().BeNull();
    }

    private async Task<byte[]?> ReadOutboxEnvelope(Guid submissionId)
    {
        await using var cmd = _database.DataSource.CreateCommand(
            "SELECT payload_envelope FROM outbox_messages WHERE aggregate_id = @aggregate_id AND event_type = @event_type;");
        cmd.Parameters.Add("aggregate_id", NpgsqlDbType.Uuid).Value = submissionId;
        cmd.Parameters.Add("event_type", NpgsqlDbType.Text).Value = QrOrderingIntegrationEventTypes.QrOrderSubmitted;
        var result = await cmd.ExecuteScalarAsync();
        return result as byte[];
    }

    private async Task<long> CountOutboxMessages(Guid submissionId)
    {
        await using var cmd = _database.DataSource.CreateCommand(
            "SELECT count(*) FROM outbox_messages WHERE aggregate_id = @aggregate_id AND event_type = @event_type;");
        cmd.Parameters.Add("aggregate_id", NpgsqlDbType.Uuid).Value = submissionId;
        cmd.Parameters.Add("event_type", NpgsqlDbType.Text).Value = QrOrderingIntegrationEventTypes.QrOrderSubmitted;
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}
