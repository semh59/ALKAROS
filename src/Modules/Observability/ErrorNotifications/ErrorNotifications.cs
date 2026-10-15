using System.Net;
using System.Text.Json;
using ALKAROS.Observability.ErrorEvents;
using Npgsql;

namespace ALKAROS.Observability.ErrorNotifications;

public enum NotificationOutcome { Sent, RetryLater, Rejected }

public sealed record NotificationSendResult(NotificationOutcome Outcome, TimeSpan? RetryAfter = null);

/// <summary>A delivery channel for already formatted fixed-token text.</summary>
public interface INotificationChannel
{
    bool IsConfigured { get; }

    Task<NotificationSendResult> SendAsync(string text, CancellationToken cancellationToken);
}

public sealed record NotificationItem(
    long Id, string Fingerprint, string Category, string Severity, string? TerminalLabel,
    string Kind, int Attempts, DateTimeOffset EnqueuedAt);

public sealed record DispatchSummary(int Sent, int Suppressed, int Failed, int Digested, bool ChannelConfigured);

/// <summary>Builds the only text that leaves the system: category, count, time and terminal label.</summary>
public static class NotificationText
{
    public static string Critical(NotificationItem item)
    {
        var terminal = string.IsNullOrEmpty(item.TerminalLabel) ? string.Empty : $" | {item.TerminalLabel}";
        return $"KRITIK: {item.Category} | {item.EnqueuedAt.ToLocalTime():HH:mm}{terminal}";
    }

    public static string Digest(IReadOnlyCollection<NotificationItem> items, DateTimeOffset now)
    {
        var lines = items
            .GroupBy(i => i.Category, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => $"{g.Key} x{g.Count()}");
        return $"OZET: {items.Count} bildirim | {now.ToLocalTime():HH:mm}\n{string.Join('\n', lines)}";
    }
}

/// <summary>
/// Persistent outgoing queue. Critical errors are sent one by one, everything else leaves as a periodic digest;
/// a pile-up (for example after the network returns) collapses into a single digest message.
/// </summary>
public sealed class NotificationEnqueuer(
    NpgsqlDataSource dataSource, Func<DateTimeOffset>? clock = null) : IErrorEventChangeSink
{
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);

    public async Task OnChangesAsync(IReadOnlyList<ErrorEventChange> changes, CancellationToken cancellationToken)
    {
        var now = _clock();
        foreach (var change in changes.Where(c => c.Severity >= ErrorSeverity.Warning))
        {
            var kind = change.Severity == ErrorSeverity.Critical ? "Critical" : "Digest";
            await using var command = dataSource.CreateCommand(
                """
                INSERT INTO observability.notification_queue
                    (fingerprint, layer, category, severity, terminal_label, kind, enqueued_at, next_attempt_at)
                SELECT @fp, @layer, @cat, @sev,
                       (SELECT last_terminal_label FROM observability.error_events WHERE fingerprint = @fp),
                       @kind, @now, @now;
                """);
            command.Parameters.AddWithValue("fp", change.Fingerprint);
            command.Parameters.AddWithValue("layer", change.Layer.ToString());
            command.Parameters.AddWithValue("cat", change.Category);
            command.Parameters.AddWithValue("sev", change.Severity.ToString());
            command.Parameters.AddWithValue("kind", kind);
            command.Parameters.AddWithValue("now", now.UtcDateTime);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

public sealed class NotificationDispatcher(
    NpgsqlDataSource dataSource,
    INotificationChannel channel,
    Func<DateTimeOffset>? clock = null,
    int maxAttempts = 3,
    int floodThreshold = 20,
    TimeSpan? suppressionWindow = null,
    TimeSpan? digestInterval = null)
{
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly TimeSpan _suppression = suppressionWindow ?? TimeSpan.FromMinutes(30);
    private readonly TimeSpan _digestInterval = digestInterval ?? TimeSpan.FromMinutes(5);

    public async Task<DispatchSummary> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!channel.IsConfigured)
            return new DispatchSummary(0, 0, 0, 0, false);

        var now = _clock();
        var due = await LoadDueAsync(now, cancellationToken);
        int sent = 0, suppressed = 0, failed = 0, digested = 0;

        if (due.Count > floodThreshold)
        {
            var result = await TrySendAsync(NotificationText.Digest(due, now), cancellationToken);
            if (result.Outcome == NotificationOutcome.Sent)
            {
                await SetStateAsync(due.Select(i => i.Id), "Digested", now, cancellationToken);
                return new DispatchSummary(1, 0, 0, due.Count, true);
            }

            failed += await RetryAsync(due, result, now, cancellationToken);
            return new DispatchSummary(0, 0, failed, 0, true);
        }

        foreach (var item in due.Where(i => i.Kind == "Critical"))
        {
            if (await WasSentRecentlyAsync(item.Fingerprint, now, cancellationToken))
            {
                await SetStateAsync([item.Id], "Suppressed", now, cancellationToken);
                suppressed++;
                continue;
            }

            var result = await TrySendAsync(NotificationText.Critical(item), cancellationToken);
            if (result.Outcome == NotificationOutcome.Sent)
            {
                await SetStateAsync([item.Id], "Sent", now, cancellationToken);
                await MarkSentAsync(item.Fingerprint, now, cancellationToken);
                sent++;
            }
            else
            {
                failed += await RetryAsync([item], result, now, cancellationToken);
            }
        }

        var digest = due.Where(i => i.Kind == "Digest").ToList();
        if (digest.Count > 0 && now - digest.Min(i => i.EnqueuedAt) >= _digestInterval)
        {
            var result = await TrySendAsync(NotificationText.Digest(digest, now), cancellationToken);
            if (result.Outcome == NotificationOutcome.Sent)
            {
                await SetStateAsync(digest.Select(i => i.Id), "Digested", now, cancellationToken);
                digested += digest.Count;
                sent++;
            }
            else
            {
                failed += await RetryAsync(digest, result, now, cancellationToken);
            }
        }

        return new DispatchSummary(sent, suppressed, failed, digested, true);
    }

    private async Task<NotificationSendResult> TrySendAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            return await channel.SendAsync(text, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return new NotificationSendResult(NotificationOutcome.RetryLater);
        }
    }

    private async Task<int> RetryAsync(
        IEnumerable<NotificationItem> items, NotificationSendResult result, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var failed = 0;
        foreach (var item in items)
        {
            var attempts = item.Attempts + 1;
            var exhausted = attempts >= maxAttempts;
            var wait = result.RetryAfter ?? TimeSpan.FromSeconds(30 * attempts);
            await using var command = dataSource.CreateCommand(
                "UPDATE observability.notification_queue SET attempts = @a, state = @s, next_attempt_at = @n WHERE id = @id;");
            command.Parameters.AddWithValue("a", attempts);
            command.Parameters.AddWithValue("s", exhausted ? "Failed" : "Pending");
            command.Parameters.AddWithValue("n", (now + wait).UtcDateTime);
            command.Parameters.AddWithValue("id", item.Id);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (exhausted)
                failed++;
        }

        return failed;
    }

    private async Task<List<NotificationItem>> LoadDueAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT id, fingerprint, category, severity, terminal_label, kind, attempts, enqueued_at
            FROM observability.notification_queue
            WHERE state = 'Pending' AND next_attempt_at <= @now
            ORDER BY id LIMIT 1000;
            """);
        command.Parameters.AddWithValue("now", now.UtcDateTime);
        var items = new List<NotificationItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(new NotificationItem(
                reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetInt32(6),
                new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc))));
        }

        return items;
    }

    private async Task<bool> WasSentRecentlyAsync(string fingerprint, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT EXISTS (SELECT 1 FROM observability.notification_suppressions WHERE fingerprint = @f AND last_sent_at > @since);");
        command.Parameters.AddWithValue("f", fingerprint);
        command.Parameters.AddWithValue("since", (now - _suppression).UtcDateTime);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    private async Task MarkSentAsync(string fingerprint, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            INSERT INTO observability.notification_suppressions (fingerprint, last_sent_at) VALUES (@f, @n)
            ON CONFLICT (fingerprint) DO UPDATE SET last_sent_at = EXCLUDED.last_sent_at;
            """);
        command.Parameters.AddWithValue("f", fingerprint);
        command.Parameters.AddWithValue("n", now.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SetStateAsync(IEnumerable<long> ids, string state, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE observability.notification_queue SET state = @s, sent_at = @n WHERE id = ANY(@ids);");
        command.Parameters.AddWithValue("s", state);
        command.Parameters.AddWithValue("n", now.UtcDateTime);
        command.Parameters.AddWithValue("ids", ids.ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Telegram Bot API channel. Credentials come from a callback so the secret store stays outside this module;
/// no failure path ever carries the request address, so the bot key cannot reach a log or an error text.
/// </summary>
public sealed class TelegramChannel(HttpClient client, Func<(string? BotKey, string? ChatId)> credentials) : INotificationChannel
{
    public bool IsConfigured
    {
        get
        {
            var (key, chat) = credentials();
            return !string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(chat);
        }
    }

    public async Task<NotificationSendResult> SendAsync(string text, CancellationToken cancellationToken)
    {
        var (key, chat) = credentials();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(chat))
            return new NotificationSendResult(NotificationOutcome.Rejected);

        try
        {
            using var body = new FormUrlEncodedContent(new Dictionary<string, string> { ["chat_id"] = chat, ["text"] = text });
            using var response = await client.PostAsync(
                new Uri($"https://api.telegram.org/bot{key}/sendMessage"), body, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return new NotificationSendResult(NotificationOutcome.Sent);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return new NotificationSendResult(NotificationOutcome.RetryLater, await RetryAfterAsync(response, cancellationToken));

            return new NotificationSendResult(
                (int)response.StatusCode >= 500 ? NotificationOutcome.RetryLater : NotificationOutcome.Rejected);
        }
        catch (HttpRequestException)
        {
            return new NotificationSendResult(NotificationOutcome.RetryLater);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new NotificationSendResult(NotificationOutcome.RetryLater);
        }
    }

    private static async Task<TimeSpan> RetryAfterAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
            return delta;

        try
        {
            using var json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken);
            if (json.RootElement.TryGetProperty("parameters", out var parameters)
                && parameters.TryGetProperty("retry_after", out var seconds))
                return TimeSpan.FromSeconds(seconds.GetInt32());
        }
        catch (JsonException)
        {
            return TimeSpan.FromSeconds(30);
        }

        return TimeSpan.FromSeconds(30);
    }
}
