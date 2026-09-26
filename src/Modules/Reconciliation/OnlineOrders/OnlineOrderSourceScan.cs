using System.Globalization;
using ALKAROS.Reconciliation.Payments;
using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// What every online order source pair shares. The sources are read by plain SQL across schemas (the same
/// read-model pattern V13-REC-001 uses for payments), never through a reference to the OnlineOrdering or
/// Orders modules, so Reconciliation keeps depending on nothing but itself.
/// </summary>
internal static class OnlineOrderSourceScan
{
    /// <summary>A scan larger than this fails loud instead of silently reporting a partial picture.</summary>
    public const int MaxScanRows = 5000;

    /// <summary>
    /// The outbox event type V12-ONL-003 writes for every status update meant for Yemeksepeti
    /// (<c>YemeksepetiStatusSync.StatusUpdateRequestedEventType</c>), repeated here because this module may
    /// not reference OnlineOrdering. A rename there without one here makes the V12-REC-001 tests fail.
    /// </summary>
    public const string StatusUpdateEventType = "online-ordering.yemeksepeti.status-update-requested.v1";

    public static async Task<IReadOnlyList<DetectedDiscrepancy>> ReadAsync(
        NpgsqlCommand command,
        string sourceName,
        Func<NpgsqlDataReader, DetectedDiscrepancy> map,
        CancellationToken cancellationToken)
    {
        var results = new List<DetectedDiscrepancy>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(map(reader));
        }

        if (results.Count > MaxScanRows)
            throw new OnlineOrderScanTooLargeException(sourceName);
        return results;
    }
}

/// <summary>V12-RMD-006: a source had more divergences than one scan reads; reported as such, not as unreadable.</summary>
internal sealed class OnlineOrderScanTooLargeException : Exception
{
    public OnlineOrderScanTooLargeException(string sourceName)
        : base(string.Create(CultureInfo.InvariantCulture,
            $"{sourceName} scan returned more than {OnlineOrderSourceScan.MaxScanRows} rows; narrow the filter or paginate."))
    {
    }
}
