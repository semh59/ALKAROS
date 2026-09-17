namespace ALKAROS.Payments.TenderRouting;

/// <summary>
/// A request to tender a Payment through one of the canonical
/// <see cref="TenderRouting.TenderMethod"/> values. The envelope every
/// handler (Cash/BankCard/MealCard, later) and the router itself agree on.
/// </summary>
public sealed record TenderRequest(
    Guid PaymentId,
    TenderMethod Method,
    decimal Amount)
{
    public void Validate()
    {
        if (PaymentId == Guid.Empty)
            throw new ArgumentException("Payment id cannot be empty.", nameof(PaymentId));
        if (Amount <= 0)
            throw new ArgumentException("Tender amount must be greater than zero.", nameof(Amount));
    }
}
