namespace ALKAROS.Payments.CardSettlement;

/// <summary>
/// The persisted outcome of a card settlement attempt (payments.
/// card_settlement_attempts.outcome). Mirrors the three
/// <c>ALKAROS.Payments.TenderRouting.TenderHandlerResult</c> cases this
/// orchestrator consumes, but as a plain enum so it can be stored as a
/// column and compared on replay without depending on TenderRouting's
/// record types at the persistence layer.
/// </summary>
public enum CardSettlementOutcome
{
    Approved,
    Declined,
    RequiresReconciliation,
}
