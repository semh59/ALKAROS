namespace ALKAROS.Payments.TenderRouting;

/// <summary>
/// Registered for <see cref="TenderMethod.BankCard"/> in real production
/// composition ONLY because V13-PAY-003's own Acceptance evidence requires
/// both Cash and BankCard to resolve at startup, even though the real
/// terminal integration (V13-HUG-001, Token/Beko) does not exist yet and is
/// explicitly forbidden from getting even a schema/stub/dead-code
/// implementation (2026-09-22 Semih-approved deferral, `plan/GATES.md`'s
/// `V13_EXIT_ENTRY_WAIVER` table) — real client-id/terminal credentials
/// don't exist to build or verify a real adapter against.
///
/// This is NOT that adapter and never produces a fabricated Approved or
/// Declined outcome (CORR:C29 "never guess"/never fake a payment result) —
/// every attempt honestly reports that it needs manual reconciliation,
/// because no real terminal call was ever made. It exists purely so this
/// task's own fail-closed registry-composition acceptance criterion can be
/// satisfied literally without lying about payment outcomes.
///
/// Delete this class and its registration in
/// <see cref="TenderCompositionModule"/> the day V13-HUG-001 ships a real
/// <see cref="ITenderHandler"/> for <see cref="TenderMethod.BankCard"/> —
/// do not mistake this for finished BankCard support.
/// </summary>
public sealed class PendingBankCardTerminalIntegrationHandler : ITenderHandler
{
    public const string PendingIntegrationReason =
        "Token/Beko terminal entegrasyonu (V13-HUG-001) henüz tamamlanmadı; " +
        "bu kart ödemesi elle mutabakat gerektirir.";

    public TenderMethod Method => TenderMethod.BankCard;

    public Task<TenderHandlerResult> HandleAsync(
        TenderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Method != TenderMethod.BankCard)
            throw new ArgumentException(
                $"'{nameof(PendingBankCardTerminalIntegrationHandler)}' only handles '{TenderMethod.BankCard}' requests.",
                nameof(request));

        return Task.FromResult<TenderHandlerResult>(
            new TenderRequiresReconciliation(PendingIntegrationReason));
    }
}
