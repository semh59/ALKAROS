namespace ALKAROS.Payments.TenderRouting;

/// <summary>
/// The outcome a tender handler (Cash/BankCard/MealCard) hands back to the
/// router — normalized so a composing caller (V13-PAY-003) can turn any of
/// them into the matching <c>Payment.Approve</c>/<c>Decline</c>/
/// <c>MarkUnknown</c> call without knowing which tender method produced it.
/// </summary>
public abstract record TenderHandlerResult;

/// <summary>The tender was approved for <paramref name="ApprovedAmount"/> (maps to <c>Payment.Approve</c>).</summary>
public sealed record TenderApproved(decimal ApprovedAmount) : TenderHandlerResult;

/// <summary>The tender was declined outright (maps to <c>Payment.Decline</c>).</summary>
public sealed record TenderDeclined(string Reason) : TenderHandlerResult;

/// <summary>
/// The tender's outcome could not be determined synchronously (a provider
/// timeout) — never an implicit approval or decline (CORR:C29). Maps to
/// <c>Payment.MarkUnknown</c>.
/// </summary>
public sealed record TenderRequiresReconciliation(string Reason) : TenderHandlerResult;
