using System.Net;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Observability.ErrorNotifications.Tests;

public sealed class NotificationsDatabase : PgTestDatabase
{
    public NotificationsDatabase() : base("alkaros_rmd538_") { }

    protected override async Task ApplySqlAsync()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        foreach (var migration in new[] { ("V1-RMD-533", "187-*.up.sql"), ("V1-RMD-538", "188-*.up.sql") })
        {
            var file = Directory.GetFiles(Path.Combine(root, "database", "migrations", "V1", migration.Item1), migration.Item2).Single();
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        }
    }
}

public sealed class ErrorNotificationsTests : IClassFixture<NotificationsDatabase>
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly NotificationsDatabase _database;
    private DateTimeOffset _now = T0;

    public ErrorNotificationsTests(NotificationsDatabase database) => _database = database;

    private sealed class FakeChannel : INotificationChannel
    {
        public bool IsConfigured { get; set; } = true;
        public Queue<NotificationSendResult> Results { get; } = new();
        public List<string> Texts { get; } = [];

        public Task<NotificationSendResult> SendAsync(string text, CancellationToken cancellationToken)
        {
            Texts.Add(text);
            return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : new NotificationSendResult(NotificationOutcome.Sent));
        }
    }

    private static ErrorEventChange Change(string fingerprint, ErrorSeverity severity, string category = "db.down") =>
        new(fingerprint, ErrorLayer.Database, severity, category, "secret detail text", true, false, 1);

    private async Task ResetAsync() =>
        await _database.ExecuteAsync("TRUNCATE observability.notification_queue, observability.notification_suppressions;");

    private NotificationEnqueuer Queue() => new(_database.DataSource, () => _now);

    private NotificationDispatcher Dispatcher(INotificationChannel channel) =>
        new(_database.DataSource, channel, () => _now);

    private Task<long> CountStateAsync(string state) =>
        _database.ScalarAsync<long>($"SELECT count(*) FROM observability.notification_queue WHERE state = '{state}';");

    [Fact]
    public async Task CriticalChangeIsQueuedAndSentOnceWithFixedTokensOnly()
    {
        await ResetAsync();
        var channel = new FakeChannel();
        await Queue().OnChangesAsync([Change("fp1", ErrorSeverity.Critical)], CancellationToken.None);

        var summary = await Dispatcher(channel).RunOnceAsync();

        summary.Sent.Should().Be(1);
        channel.Texts.Should().ContainSingle().Which.Should().StartWith("KRITIK: db.down").And.NotContain("secret");
        (await CountStateAsync("Sent")).Should().Be(1);
    }

    [Fact]
    public async Task RepeatOfSameFingerprintInsideWindowIsSuppressed()
    {
        await ResetAsync();
        var channel = new FakeChannel();
        await Queue().OnChangesAsync([Change("fp2", ErrorSeverity.Critical)], CancellationToken.None);
        await Dispatcher(channel).RunOnceAsync();
        await Queue().OnChangesAsync([Change("fp2", ErrorSeverity.Critical)], CancellationToken.None);

        var summary = await Dispatcher(channel).RunOnceAsync();

        summary.Suppressed.Should().Be(1);
        channel.Texts.Should().HaveCount(1);
    }

    [Fact]
    public async Task HundredPendingNotificationsBecomeOneDigestMessage()
    {
        await ResetAsync();
        var channel = new FakeChannel();
        var changes = Enumerable.Range(0, 100).Select(i => Change($"flood{i}", ErrorSeverity.Critical, "api.flood")).ToList();
        await Queue().OnChangesAsync(changes, CancellationToken.None);

        var summary = await Dispatcher(channel).RunOnceAsync();

        channel.Texts.Should().ContainSingle().Which.Should().StartWith("OZET: 100").And.Contain("api.flood x100");
        summary.Digested.Should().Be(100);
        (await CountStateAsync("Digested")).Should().Be(100);
    }

    [Fact]
    public async Task FailedSendRetriesThreeTimesThenMarksFailed()
    {
        await ResetAsync();
        var channel = new FakeChannel();
        for (var i = 0; i < 3; i++)
            channel.Results.Enqueue(new NotificationSendResult(NotificationOutcome.RetryLater));
        await Queue().OnChangesAsync([Change("fp3", ErrorSeverity.Critical)], CancellationToken.None);

        for (var i = 0; i < 3; i++)
        {
            await Dispatcher(channel).RunOnceAsync();
            _now = _now.AddMinutes(10);
        }

        channel.Texts.Should().HaveCount(3);
        (await CountStateAsync("Failed")).Should().Be(1);
        (await Dispatcher(channel).RunOnceAsync()).Sent.Should().Be(0);
    }

    [Fact]
    public async Task TooManyRequestsWaitsForRetryAfterBeforeNextAttempt()
    {
        await ResetAsync();
        var channel = new FakeChannel();
        channel.Results.Enqueue(new NotificationSendResult(NotificationOutcome.RetryLater, TimeSpan.FromMinutes(5)));
        await Queue().OnChangesAsync([Change("fp4", ErrorSeverity.Critical)], CancellationToken.None);

        await Dispatcher(channel).RunOnceAsync();
        _now = _now.AddMinutes(2);
        await Dispatcher(channel).RunOnceAsync();
        channel.Texts.Should().HaveCount(1);

        _now = _now.AddMinutes(4);
        await Dispatcher(channel).RunOnceAsync();
        channel.Texts.Should().HaveCount(2);
    }

    [Fact]
    public async Task UnconfiguredChannelSendsNothingAndKeepsQueue()
    {
        await ResetAsync();
        var channel = new FakeChannel { IsConfigured = false };
        await Queue().OnChangesAsync([Change("fp5", ErrorSeverity.Critical)], CancellationToken.None);

        var summary = await Dispatcher(channel).RunOnceAsync();

        summary.ChannelConfigured.Should().BeFalse();
        channel.Texts.Should().BeEmpty();
        (await CountStateAsync("Pending")).Should().Be(1);
    }

    [Fact]
    public async Task DigestItemsWaitForTheDigestInterval()
    {
        await ResetAsync();
        var channel = new FakeChannel();
        await Queue().OnChangesAsync([Change("fp6", ErrorSeverity.Warning, "api.slow_request")], CancellationToken.None);

        await Dispatcher(channel).RunOnceAsync();
        channel.Texts.Should().BeEmpty();

        _now = _now.AddMinutes(6);
        await Dispatcher(channel).RunOnceAsync();
        channel.Texts.Should().ContainSingle().Which.Should().Contain("api.slow_request x1");
    }

    [Fact]
    public async Task InfoSeverityIsNeverQueued()
    {
        await ResetAsync();
        await Queue().OnChangesAsync([Change("fp7", ErrorSeverity.Info)], CancellationToken.None);

        (await _database.ScalarAsync<long>("SELECT count(*) FROM observability.notification_queue;")).Should().Be(0);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException($"connection failed for {request.RequestUri}");
    }

    private sealed class StatusHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    [Fact]
    public async Task TelegramFailureNeverCarriesTheBotKey()
    {
        const string key = "123456:SECRET-BOT-KEY";
        using var client = new HttpClient(new ThrowingHandler());
        var channel = new TelegramChannel(client, () => (key, "42"));

        var result = await channel.SendAsync("KRITIK: x", CancellationToken.None);

        result.Outcome.Should().Be(NotificationOutcome.RetryLater);
        result.ToString().Should().NotContain("SECRET");
    }

    [Fact]
    public async Task TelegramTooManyRequestsReadsRetryAfterFromBody()
    {
        using var client = new HttpClient(new StatusHandler(HttpStatusCode.TooManyRequests, "{\"parameters\":{\"retry_after\":17}}"));
        var channel = new TelegramChannel(client, () => ("k", "42"));

        var result = await channel.SendAsync("x", CancellationToken.None);

        result.Outcome.Should().Be(NotificationOutcome.RetryLater);
        result.RetryAfter.Should().Be(TimeSpan.FromSeconds(17));
    }

    [Fact]
    public async Task TelegramWithoutCredentialsIsNotConfigured()
    {
        using var client = new HttpClient(new StatusHandler(HttpStatusCode.OK, "{}"));

        new TelegramChannel(client, () => (null, "42")).IsConfigured.Should().BeFalse();
        new TelegramChannel(client, () => ("k", "42")).IsConfigured.Should().BeTrue();
        await Task.CompletedTask;
    }
}
