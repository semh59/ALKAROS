using System.Text.RegularExpressions;
using Npgsql;

namespace ALKAROS.Observability.TelemetryPrivacy;

/// <summary>One monitoring table and how long its rows are kept.</summary>
public sealed record TelemetryRetentionPolicy(string Table, string TimeColumn, int RetentionDays);

public static class TelemetryRetention
{
    public const int RawEventDays = 30;
    public const int HourlySummaryDays = 395;
    public const int ErrorEventDays = 90;

    /// <summary>Raw click-level tables carry this prefix; they are kept out of the database backup.</summary>
    public const string RawTablePrefix = "raw_";

    private static readonly Regex Identifier = new(@"^[a-z_][a-z0-9_]*\.[a-z_][a-z0-9_]*$", RegexOptions.Compiled);

    public static bool IsRawTable(string qualifiedTable) =>
        qualifiedTable.Substring(qualifiedTable.IndexOf('.') + 1).StartsWith(RawTablePrefix, StringComparison.Ordinal);

    /// <summary>Deletes rows older than each policy's retention in bounded batches; returns rows removed per table.</summary>
    public static async Task<IReadOnlyDictionary<string, int>> PurgeAsync(
        NpgsqlDataSource dataSource,
        IEnumerable<TelemetryRetentionPolicy> policies,
        DateTimeOffset now,
        int batchSize = 5000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        var removed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var policy in policies)
        {
            if (!Identifier.IsMatch(policy.Table) || !Regex.IsMatch(policy.TimeColumn, "^[a-z_][a-z0-9_]*$"))
                throw new ArgumentException($"Unsafe table or column name: {policy.Table}.{policy.TimeColumn}");
            if (policy.RetentionDays < 1)
                throw new ArgumentOutOfRangeException(nameof(policies), "Retention must be at least one day.");

            var cutoff = now.UtcDateTime.AddDays(-policy.RetentionDays);
            var total = 0;
            while (true)
            {
                await using var command = dataSource.CreateCommand(
                    $"""
                    WITH doomed AS (
                        SELECT ctid FROM {policy.Table} WHERE {policy.TimeColumn} < $1 LIMIT {batchSize}
                    )
                    DELETE FROM {policy.Table} WHERE ctid IN (SELECT ctid FROM doomed);
                    """);
                command.Parameters.AddWithValue(cutoff);
                var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                total += affected;
                if (affected < batchSize)
                    break;
            }

            removed[policy.Table] = total;
        }

        return removed;
    }
}
