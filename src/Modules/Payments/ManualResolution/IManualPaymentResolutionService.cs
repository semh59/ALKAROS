using Npgsql;

namespace ALKAROS.Payments.ManualResolution;

/// <summary>
/// Runs inside the resolution's own transaction, just before it commits: the caller writes its audit record here, so an
/// audit record that cannot be written leaves the payment unresolved.
/// </summary>
public delegate Task InTransactionRecorder<in TResult>(
    TResult result, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken);

/// <summary>The observable outcome of a manual resolution.</summary>
public sealed record ManualPaymentResolutionResult(Guid PaymentId, Guid BillId, string PreviousStatus, string NewStatus);

/// <summary>
/// V1-RMD-264: lets an authorized manager close a card payment whose outcome
/// the system could not confirm. No real card terminal exists (V13-HUG-001
/// stays forbidden), so an unresolved BankCard payment would otherwise lock
/// its bill forever. Only the "the card was NOT charged" resolution exists
/// here: it creates no money record, so a wrong call at worst makes the
/// customer pay again. Authorization is the caller's job; this service
/// enforces state, reason and concurrency.
/// </summary>
public interface IManualPaymentResolutionService
{
    Task<ManualPaymentResolutionResult> MarkNotChargedAsync(
        Guid paymentId, Guid actorUserId, string reason, InTransactionRecorder<ManualPaymentResolutionResult> record,
        CancellationToken cancellationToken = default);
}
