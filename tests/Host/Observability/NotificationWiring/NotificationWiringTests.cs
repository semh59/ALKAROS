using System.Reflection;
using ALKAROS.Audit.EventStore;
using ALKAROS.Host.Observability.NotificationWiring;
using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Observability.AlertFoundation;
using Microsoft.Extensions.DependencyInjection;
using ALKAROS.Host.Observability.ServerDetectors;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.Observability.ErrorNotifications;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ALKAROS.Host.Observability.NotificationWiring.Tests;

public sealed class WiringDatabase : PgTestDatabase
{
    public WiringDatabase() : base("alkaros_rmd561_") { }

    protected override async Task ApplySqlAsync()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        foreach (var (folder, pattern) in new[] { ("V1-RMD-533", "187-*.up.sql"), ("V1-RMD-538", "188-*.up.sql") })
        {
            var file = Directory.GetFiles(Path.Combine(root, "database", "migrations", "V1", folder), pattern).Single();
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        }
    }
}

public sealed class NotificationWiringTests : IClassFixture<WiringDatabase>
{
    private readonly WiringDatabase _database;

    public NotificationWiringTests(WiringDatabase database) => _database = database;

    private sealed class FakeChannel : INotificationChannel
    {
        public bool IsConfigured => true;
        public List<string> Texts { get; } = [];

        public Task<NotificationSendResult> SendAsync(string text, CancellationToken cancellationToken)
        {
            Texts.Add(text);
            return Task.FromResult(new NotificationSendResult(NotificationOutcome.Sent));
        }
    }

    private sealed class ThrowingSink : IErrorEventChangeSink
    {
        public Task OnChangesAsync(IReadOnlyList<ErrorEventChange> changes, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("sink down");
    }

    private sealed class CountingSink : IErrorEventChangeSink
    {
        public int Calls { get; private set; }

        public Task OnChangesAsync(IReadOnlyList<ErrorEventChange> changes, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task CriticalEventReachesTheQueueAndTheDispatcherSendsOneMessage()
    {
        await _database.ExecuteAsync("DELETE FROM observability.notification_queue;");
        var sink = new CompositeErrorEventSink(new NotificationEnqueuer(_database.DataSource));
        await using var writer = new ErrorEventWriter(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5), sink: sink);
        var channel = new FakeChannel();

        writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Database, ErrorSeverity.Critical, "db.down", "secret", TerminalLabel: "terminal:ab12cd34"));
        await writer.FlushAsync();
        var summary = await new NotificationDispatcher(_database.DataSource, channel).RunOnceAsync();

        summary.Sent.Should().Be(1);
        channel.Texts.Should().ContainSingle().Which.Should().StartWith("KRITIK: db.down").And.Contain("terminal:ab12cd34").And.NotContain("secret");
    }

    [Fact]
    public async Task FailingSinkDoesNotStopTheOthers()
    {
        var counting = new CountingSink();
        var composite = new CompositeErrorEventSink(new ThrowingSink(), counting, null);

        var act = () => composite.OnChangesAsync([], CancellationToken.None);

        await act.Should().ThrowAsync<AggregateException>();
        counting.Calls.Should().Be(1);
    }

    [Fact]
    public void ChannelIsOffWithoutBothSettingsAndOnWithThem()
    {
        var empty = new ConfigurationBuilder().Build();
        var configured = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [NotificationWiringExtensions.BotKeySetting] = "k",
            [NotificationWiringExtensions.ChatIdSetting] = "42",
        }).Build();
        var onlyKey = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [NotificationWiringExtensions.BotKeySetting] = "k",
        }).Build();

        using var client = new HttpClient();
        new TelegramChannel(client, () => NotificationWiringExtensions.Credentials(empty)).IsConfigured.Should().BeFalse();
        new TelegramChannel(client, () => NotificationWiringExtensions.Credentials(onlyKey)).IsConfigured.Should().BeFalse();
        new TelegramChannel(client, () => NotificationWiringExtensions.Credentials(configured)).IsConfigured.Should().BeTrue();
    }

    public class CompletedProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var type = targetMethod!.ReturnType;
            if (type == typeof(Task))
                return Task.CompletedTask;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type.GetGenericArguments()[0]).Invoke(null, [null]);
            return null;
        }
    }

    [Fact]
    public async Task HostRegistrationFeedsTheNotificationQueueFromTheRealWriter()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(new MonitoringDataSource(_database.DataSource));
        services.AddSingleton(DispatchProxy.Create<IAlertService, CompletedProxy>());
        services.AddSingleton(DispatchProxy.Create<IAuditEventStore, CompletedProxy>());
        services.AddServerErrorDetection();
        services.AddNotificationDelivery();
        await using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<ErrorEventWriter>();
        var before = await _database.ScalarAsync<long>("SELECT count(*) FROM observability.notification_queue;");

        writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Api, ErrorSeverity.Critical, "api.wiring_check", "x"));
        await writer.FlushAsync();

        (await _database.ScalarAsync<long>("SELECT count(*) FROM observability.notification_queue;")).Should().Be(before + 1);
    }
}
