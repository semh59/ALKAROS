using Npgsql;

namespace ALKAROS.Billing.BillFoundation;

public enum ReceiptPrintOutcome
{
    NotFound,
    NotPaid,
    Original,
    Copy,
}

/// <summary>What a print request found; <see cref="OriginalPrintedAt"/> is set for <see cref="ReceiptPrintOutcome.Original"/> and <see cref="ReceiptPrintOutcome.Copy"/>.</summary>
public readonly record struct ReceiptPrintClaim(ReceiptPrintOutcome Outcome, DateTimeOffset? OriginalPrintedAt);

public enum ReceiptResetOutcome
{
    NotFound,
    NothingToReset,
    Reset,
}

/// <summary>What a reset found; the previous print time and printer are set for <see cref="ReceiptResetOutcome.Reset"/>.</summary>
public readonly record struct ReceiptResetClaim(ReceiptResetOutcome Outcome, DateTimeOffset? PreviousPrintedAt, Guid? PreviousPrintedBy);

public interface IBillReceiptPrintRepository
{
    /// <summary>When the original information receipt of the bill was printed; <c>null</c> when it has not been or the bill does not exist.</summary>
    Task<DateTimeOffset?> FindOriginalPrintedAtAsync(Guid billId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Under a row lock in the caller's transaction, checks the bill is paid and marks its receipt as printed by
    /// <paramref name="userId"/> unless an original was printed already. Of any number of simultaneous prints exactly one
    /// gets <see cref="ReceiptPrintOutcome.Original"/>; the others wait on the lock and see the time of the winner. A bill
    /// that is missing or not paid is left untouched.
    /// </summary>
    Task<ReceiptPrintClaim> ClaimOriginalAsync(
        Guid billId, Guid userId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Under the same row lock, forgets that an original was printed so the next print is the original again; the previous
    /// print time and printer are returned for the caller's audit record. A bill whose original was never printed is left untouched.
    /// </summary>
    Task<ReceiptResetClaim> ResetOriginalAsync(
        Guid billId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default);
}
