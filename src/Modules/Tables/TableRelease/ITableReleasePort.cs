using Npgsql;

namespace ALKAROS.Tables.TableRelease;

/// <summary>
/// The only way another area frees a table or clears its bill pointer: the Tables module owns those rows, the
/// caller owns the transaction, so the table changes commit with the order or bill change that caused them.
/// </summary>
public interface ITableReleasePort
{
    /// <summary>Clears the table pointer of a bill that is no longer the table's current bill.</summary>
    Task ClearBillPointerAsync(
        Guid billId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Clears the table pointer of every bill of the order.</summary>
    Task ClearOrderBillPointersAsync(
        Guid orderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the tables an order was attached to. A merged table keeps its pointer on its next open order and is
    /// only freed, ending the merge with <paramref name="unmergeReason"/>, once none is left; a check sent to the till
    /// is forgotten.
    /// </summary>
    Task ReleaseOrderAsync(
        Guid orderId, string unmergeReason, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default);
}
