namespace ALKAROS.Payments.CardSettlement;

/// <summary>
/// Completes a BankCard settlement attempt's Payment transition,
/// PaymentAllocation, and fiscal handoff as one crash-safe durable
/// workflow (V13-PAY-004). Never calls a terminal itself — the caller has
/// already obtained the tender outcome from a real
/// <see cref="ALKAROS.Payments.TenderRouting.ITenderHandler"/>.
/// </summary>
public interface ICardSettlementOrchestrator
{
    Task<CardSettlementResult> HandleAsync(CardSettlementRequest request, CancellationToken cancellationToken = default);
}
