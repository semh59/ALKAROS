using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Observability.TelemetryPrivacy.Tests;

public sealed class RetentionDatabase : PgTestDatabase
{
    public RetentionDatabase() : base("alkaros_rmd549_") { }

    protected override Task ApplySqlAsync() => RunAsync(
        DataSource,
        """
        CREATE SCHEMA IF NOT EXISTS observability_test;
        CREATE TABLE observability_test.raw_events (id serial PRIMARY KEY, occurred_at timestamptz NOT NULL);
        CREATE TABLE observability_test.error_events (id serial PRIMARY KEY, occurred_at timestamptz NOT NULL);
        """);
}

public sealed class TelemetryPrivacyTests : IClassFixture<RetentionDatabase>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly RetentionDatabase _database;

    public TelemetryPrivacyTests(RetentionDatabase database) => _database = database;

    public static TheoryData<string> Secret => new()
    {
        "4111 1111 1111 1111", "kart 4111111111111111 hata", "Bearer abcdefgh12345678",
        "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dBjftJeZ4CVPmB92K27uhbUJU1p1r",
    };

    public static TheoryData<string> PersonalContext => new()
    {
        "ali@example.com", "0532 123 45 67", "+90 532 123 45 67", "10000000146",
        "TR33 0006 1005 1978 6457 8413 26", "musteri ayse.yilmaz@firma.com.tr", "tutar 125,50 TL siparis 1042",
    };

    [Theory]
    [MemberData(nameof(Secret))]
    public void ASecretLookingValueIsReplacedAndNeverStored(string value)
    {
        var clean = TelemetryPrivacyGuard.Sanitize(new Dictionary<string, string?> { ["category"] = value });

        clean["category"].Should().Be(TelemetryPrivacyGuard.Redacted);
    }

    [Theory]
    [MemberData(nameof(PersonalContext))]
    public void PersonalAndPaymentContextPassesThroughByProductDecision(string value)
    {
        var clean = TelemetryPrivacyGuard.Sanitize(new Dictionary<string, string?> { ["customer_note"] = value });

        clean["customer_note"].Should().Be(value);
    }

    [Fact]
    public void SecretNamedFieldsAreDroppedAndContextFieldsKept()
    {
        var clean = TelemetryPrivacyGuard.Sanitize(new Dictionary<string, string?>
        {
            ["action_key"] = "order.confirm",
            ["error_code"] = "PAYMENT_DECLINED",
            ["amount"] = "125.50",
            ["password"] = "x",
            ["api_key"] = "y",
            ["Authorization"] = "Bearer z",
            ["card_number"] = "4111111111111111",
            ["status_code"] = null,
        });

        clean.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["action_key"] = "order.confirm",
            ["error_code"] = "PAYMENT_DECLINED",
            ["amount"] = "125.50",
        });
    }

    [Fact]
    public void ALongValueIsClippedToTheLimit()
    {
        var clean = TelemetryPrivacyGuard.Sanitize(new Dictionary<string, string?> { ["error_type"] = new string('a', 500) });

        clean["error_type"].Length.Should().Be(TelemetryPrivacyGuard.MaxValueLength);
    }

    [Fact]
    public async Task OnlyRowsPastTheRetentionAreDeletedInBatches()
    {
        await _database.ExecuteAsync(
            """
            INSERT INTO observability_test.raw_events (occurred_at)
            SELECT @p::timestamptz - make_interval(days => d) FROM unnest(ARRAY[31, 40, 45, 60, 90, 1, 5, 29]) AS d;
            """,
            ("p", Now.UtcDateTime));
        await _database.ExecuteAsync(
            "INSERT INTO observability_test.error_events (occurred_at) VALUES (@p::timestamptz - interval '91 days'), (@p::timestamptz - interval '10 days');",
            ("p", Now.UtcDateTime));

        var removed = await TelemetryRetention.PurgeAsync(
            _database.DataSource,
            [
                new TelemetryRetentionPolicy("observability_test.raw_events", "occurred_at", TelemetryRetention.RawEventDays),
                new TelemetryRetentionPolicy("observability_test.error_events", "occurred_at", TelemetryRetention.ErrorEventDays),
            ],
            Now,
            batchSize: 2);

        removed["observability_test.raw_events"].Should().Be(5);
        removed["observability_test.error_events"].Should().Be(1);
        (await _database.CountAsync("observability_test.raw_events")).Should().Be(3);
        (await _database.CountAsync("observability_test.error_events")).Should().Be(1);
    }

    [Fact]
    public async Task AnUnsafeTableNameIsRefusedBeforeAnySql()
    {
        var act = () => TelemetryRetention.PurgeAsync(
            _database.DataSource,
            [new TelemetryRetentionPolicy("observability_test.raw_events; select 1", "occurred_at", 30)],
            Now);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("observability.raw_clicks", true)]
    [InlineData("observability.error_events", false)]
    public void RawTablesAreRecognisedByTheirPrefix(string table, bool raw) =>
        TelemetryRetention.IsRawTable(table).Should().Be(raw);
}
