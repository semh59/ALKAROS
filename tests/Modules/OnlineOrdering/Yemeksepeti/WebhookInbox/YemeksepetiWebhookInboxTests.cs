using System.Security.Cryptography;
using System.Text;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.WebhookInbox.Tests;

public sealed class WebhookInboxTestDatabase : PgTestDatabase
{
    public WebhookInboxTestDatabase() : base("alkaros_ysp_inbox_test_") { }

    protected override async Task ApplySqlAsync()
    {
        await RunSqlFileAsync("145-yemeksepeti-webhook-inbox.up.sql");
        await RunSqlFileAsync("154-provider-neutral-inbox-and-mapping.up.sql");
    }

    public async Task RunSqlFileAsync(string file) =>
        await RunAsync(DataSource, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", file)));

    public async Task<long> CountForOrderAsync(string externalOrderId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT count(*) FROM online_ordering.provider_inbox WHERE external_order_id = $1;");
        command.Parameters.AddWithValue(externalOrderId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public async Task<long> CountAllAsync()
    {
        await using var command = DataSource.CreateCommand("SELECT count(*) FROM online_ordering.provider_inbox;");
        return (long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>V12-ONL-008: the same stored event, as if another platform had received it.</summary>
    public async Task<Guid> CopyAsOtherPlatformAsync(Guid inboxId)
    {
        var copy = Guid.NewGuid();
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO online_ordering.provider_inbox
                (provider, inbox_id, event_key, external_order_id, provider_status, provider_updated_at, body_sha256, payload_envelope)
            SELECT 'test-platform', $2, event_key, external_order_id, provider_status, provider_updated_at, body_sha256, payload_envelope
            FROM online_ordering.provider_inbox WHERE inbox_id = $1;
            """);
        command.Parameters.AddWithValue(inboxId);
        command.Parameters.AddWithValue(copy);
        await command.ExecuteNonQueryAsync();
        return copy;
    }

    public async Task<byte[]> EnvelopeAsync(Guid inboxId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT payload_envelope FROM online_ordering.provider_inbox WHERE inbox_id = $1;");
        command.Parameters.AddWithValue(inboxId);
        return (byte[])(await command.ExecuteScalarAsync())!;
    }
}

public sealed class YemeksepetiWebhookInboxTests : IClassFixture<WebhookInboxTestDatabase>
{
    private const string Secret = "Basic eWVtZWtzZXBldGk6czNjcjN0";

    private readonly WebhookInboxTestDatabase _db;
    private readonly InMemorySecretProvider _secrets = new();
    private readonly YemeksepetiWebhookInbox _inbox;

    public YemeksepetiWebhookInboxTests(WebhookInboxTestDatabase db)
    {
        _db = db;
        _secrets.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        _secrets.Set(YemeksepetiWebhookInbox.WebhookSecret, Secret);
        _inbox = new YemeksepetiWebhookInbox(db.DataSource, _secrets);
    }

    private static string NewOrderId() => Guid.NewGuid().ToString("D");

    private static byte[] Payload(string orderId, string status = "RECEIVED", string? updatedAt = "2026-09-25T18:00:00Z") =>
        Encoding.UTF8.GetBytes(
            $$"""
            {"order_id":"{{orderId}}","external_order_id":"YS-{{orderId[..6]}}","status":"{{status}}",
             "transport_type":"LOGISTICS_DELIVERY",
             "customer":{"first_name":"Ayşe","last_name":"Yılmaz","phone_number":"+905551112233"},
             "sys":{{(updatedAt is null ? "{}" : $$"""{"updated_at":"{{updatedAt}}"}""")}}}
            """);

    [Theory]
    [InlineData("basic eWVtZWtzZXBldGk6czNjcjN0", null)]
    [InlineData("BASIC  eWVtZWtzZXBldGk6czNjcjN0", null)]
    [InlineData(" Basic eWVtZWtzZXBldGk6czNjcjN0 ", null)]
    [InlineData("Basic eWVtZWtzZXBldGk6WFhYWFg=", WebhookReceiptOutcome.Unauthenticated)]
    [InlineData("Bearer eWVtZWtzZXBldGk6czNjcjN0", WebhookReceiptOutcome.Unauthenticated)]
    [InlineData("eWVtZWtzZXBldGk6czNjcjN0", WebhookReceiptOutcome.Unauthenticated)]
    [InlineData("", WebhookReceiptOutcome.Unauthenticated)]
    [InlineData(null, WebhookReceiptOutcome.Unauthenticated)]
    public void TheSchemeIsCaseInsensitiveButTheCredentialAndTheSchemeItselfMustMatch(string? presented, WebhookReceiptOutcome? expected)
    {
        _inbox.Authenticate(presented).Should().Be(expected);
    }

    [Fact]
    public async Task TheCustomerNoteIsReadOnlyFromTheEncryptedPayloadCleanedAndBounded()
    {
        var orderId = NewOrderId();
        var body = Encoding.UTF8.GetBytes(
            "{\"order_id\":\"" + orderId + "\",\"status\":\"RECEIVED\",\"comment\":\" Zile basmayın\\u0007, 0555 111 22 33 \"}");
        var receipt = await _inbox.ReceiveAsync(Secret, body);
        var withoutNote = NewOrderId();
        var bare = await _inbox.ReceiveAsync(Secret, Payload(withoutNote));

        _inbox.ReadCustomerNote(await _db.EnvelopeAsync(receipt.InboxId!.Value)).Should().Be("Zile basmayın, 0555 111 22 33");
        _inbox.ReadCustomerNote(await _db.EnvelopeAsync(bare.InboxId!.Value)).Should().BeNull();
    }

    [Fact]
    public async Task AnAuthenticatedDeliveryIsStoredOnce()
    {
        var orderId = NewOrderId();

        var receipt = await _inbox.ReceiveAsync(Secret, Payload(orderId));

        receipt.Outcome.Should().Be(WebhookReceiptOutcome.Stored);
        receipt.InboxId.Should().NotBeNull();
        (await _db.CountForOrderAsync(orderId)).Should().Be(1);
    }

    [Fact]
    public async Task TheSameEventOnAnotherPlatformIsAnotherRecordAndNeverAnswersForYemeksepeti()
    {
        var orderId = NewOrderId();
        var stored = await _inbox.ReceiveAsync(Secret, Payload(orderId));
        var other = await _db.CopyAsOtherPlatformAsync(stored.InboxId!.Value);

        var repeated = await _inbox.ReceiveAsync(Secret, Payload(orderId));

        (await _db.CountForOrderAsync(orderId)).Should().Be(2);
        repeated.Outcome.Should().Be(WebhookReceiptOutcome.Duplicate);
        repeated.InboxId.Should().Be(stored.InboxId).And.NotBe(other);
    }

    [Fact]
    public async Task AProviderRetryAfterTheDurableInsertGetsTheSameSuccessfulReceipt()
    {
        var orderId = NewOrderId();
        var first = await _inbox.ReceiveAsync(Secret, Payload(orderId));

        var retry = await _inbox.ReceiveAsync(Secret, Payload(orderId));

        retry.Outcome.Should().Be(WebhookReceiptOutcome.Duplicate);
        retry.InboxId.Should().Be(first.InboxId);
        (await _db.CountForOrderAsync(orderId)).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentDuplicateDeliveriesLeaveOneInboxRecord()
    {
        var orderId = NewOrderId();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var deliveries = Enumerable.Range(0, 10).Select(async _ =>
        {
            await start.Task;
            return await _inbox.ReceiveAsync(Secret, Payload(orderId));
        }).ToList();
        start.SetResult();
        var receipts = await Task.WhenAll(deliveries);

        receipts.Count(r => r.Outcome == WebhookReceiptOutcome.Stored).Should().Be(1);
        receipts.Count(r => r.Outcome == WebhookReceiptOutcome.Duplicate).Should().Be(9);
        receipts.Select(r => r.InboxId).Distinct().Should().ContainSingle();
        (await _db.CountForOrderAsync(orderId)).Should().Be(1);
    }

    [Fact]
    public async Task EachStatusChangeOfOneOrderIsItsOwnEvent()
    {
        var orderId = NewOrderId();

        await _inbox.ReceiveAsync(Secret, Payload(orderId, "RECEIVED", "2026-09-25T18:00:00Z"));
        await _inbox.ReceiveAsync(Secret, Payload(orderId, "CANCELLED", "2026-09-25T18:05:00Z"));
        await _inbox.ReceiveAsync(Secret, Payload(orderId, "CANCELLED", "2026-09-25T18:05:00Z"));

        (await _db.CountForOrderAsync(orderId)).Should().Be(2);
    }

    [Fact]
    public async Task ADifferentStatusIsADifferentEventEvenWithTheSameUpdateTime()
    {
        var orderId = NewOrderId();

        var received = await _inbox.ReceiveAsync(Secret, Payload(orderId, "RECEIVED", "2026-09-25T18:00:00Z"));
        var cancelled = await _inbox.ReceiveAsync(Secret, Payload(orderId, "CANCELLED", "2026-09-25T18:00:00Z"));
        var withoutTime = await _inbox.ReceiveAsync(Secret, Payload(orderId, "DISPATCHED", updatedAt: null));

        new[] { received.Outcome, cancelled.Outcome, withoutTime.Outcome }
            .Should().OnlyContain(outcome => outcome == WebhookReceiptOutcome.Stored);
        (await _db.CountForOrderAsync(orderId)).Should().Be(3);
    }

    public static TheoryData<string?> WrongCredentials => new()
    {
        null,
        "",
        "Basic d3Jvbmc6d3Jvbmc=",
        Secret + "x",
        Secret[..^1]
    };

    [Theory]
    [MemberData(nameof(WrongCredentials))]
    public async Task AnUnauthenticatedDeliveryStoresNothing(string? authorization)
    {
        var orderId = NewOrderId();

        var receipt = await _inbox.ReceiveAsync(authorization, Payload(orderId));

        receipt.Should().Be(new WebhookReceipt(WebhookReceiptOutcome.Unauthenticated, null));
        (await _db.CountForOrderAsync(orderId)).Should().Be(0);
    }

    [Fact]
    public async Task WithoutAConfiguredSecretTheChannelIsClosed()
    {
        var unconfigured = new InMemorySecretProvider();
        unconfigured.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var closed = new YemeksepetiWebhookInbox(_db.DataSource, unconfigured);
        var orderId = NewOrderId();

        var receipt = await closed.ReceiveAsync(Secret, Payload(orderId));

        receipt.Outcome.Should().Be(WebhookReceiptOutcome.ChannelNotConfigured);
        (await _db.CountForOrderAsync(orderId)).Should().Be(0);
    }

    public static TheoryData<string> MalformedBodies => new()
    {
        "not json",
        "[]",
        """{"status":"RECEIVED"}""",
        """{"order_id":"","status":"RECEIVED"}""",
        """{"order_id":"abc","status":42}""",
        """{"order_id":"abc\nline","status":"RECEIVED"}""",
        """{"order_id":"abc","status":"RECEIVED","sys":{"updated_at":""}}"""
    };

    [Theory]
    [MemberData(nameof(MalformedBodies))]
    public async Task AMalformedBodyStoresNothing(string body)
    {
        var before = await _db.CountAllAsync();

        var receipt = await _inbox.ReceiveAsync(Secret, Encoding.UTF8.GetBytes(body));

        receipt.Outcome.Should().Be(WebhookReceiptOutcome.Malformed);
        (await _db.CountAllAsync()).Should().Be(before);
    }

    [Fact]
    public async Task AnOversizedBodyStoresNothing()
    {
        var before = await _db.CountAllAsync();
        var body = new byte[YemeksepetiWebhookInbox.MaxBodyBytes + 1];

        var receipt = await _inbox.ReceiveAsync(Secret, body);

        receipt.Outcome.Should().Be(WebhookReceiptOutcome.TooLarge);
        (await _db.CountAllAsync()).Should().Be(before);
    }

    [Fact]
    public async Task TheRawPayloadIsStoredOnlyAsAnEnvelopeOnlyTheInboxCanOpen()
    {
        var orderId = NewOrderId();
        var receipt = await _inbox.ReceiveAsync(Secret, Payload(orderId));
        var stored = await _db.EnvelopeAsync(receipt.InboxId!.Value);

        Encoding.UTF8.GetString(stored).Should().NotContain("+905551112233").And.NotContain("Yılmaz");

        var envelope = SensitiveEnvelope.FromPersistenceBytes(stored);
        var policy = new YemeksepetiWebhookAccessPolicy();
        var protector = new SensitivePayloadProtector(new AesGcmEnvelopeCipher(new SecretResolver(_secrets, policy)), policy);
        protector.Unprotect(envelope, new SecretReference("envelope-master-key"), YemeksepetiWebhookAccessPolicy.Accessor)
            .Fields["raw_body"].Should().Contain("+905551112233");

        var otherReader = () => protector.Unprotect(envelope, new SecretReference("envelope-master-key"), "reporting.anything");
        otherReader.Should().Throw<UnauthorizedSensitiveReadException>();
    }

    [Fact]
    public async Task TheMigrationRollsBackAndReapplies()
    {
        await _db.RunSqlFileAsync("154-provider-neutral-inbox-and-mapping.down.sql");
        await _db.RunSqlFileAsync("145-yemeksepeti-webhook-inbox.down.sql");
        await _db.RunSqlFileAsync("145-yemeksepeti-webhook-inbox.up.sql");
        await _db.RunSqlFileAsync("154-provider-neutral-inbox-and-mapping.up.sql");
        var orderId = NewOrderId();

        (await _inbox.ReceiveAsync(Secret, Payload(orderId))).Outcome.Should().Be(WebhookReceiptOutcome.Stored);
    }
}
