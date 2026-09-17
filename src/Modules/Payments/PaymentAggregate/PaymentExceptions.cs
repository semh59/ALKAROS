namespace ALKAROS.Payments.PaymentAggregate;

/// <summary>
/// Base exception for domain errors in the Payments module (V13-PAY-001).
/// </summary>
public abstract class PaymentException : Exception
{
    protected PaymentException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a requested Payment is not found.
/// </summary>
public sealed class PaymentNotFoundException : PaymentException
{
    public PaymentNotFoundException(Guid paymentId)
        : base($"Payment '{paymentId}' was not found.")
    {
        PaymentId = paymentId;
    }

    public Guid PaymentId { get; }
}

/// <summary>
/// Thrown when a requested lifecycle transition is not in the canonical
/// Payment transition matrix (V0-DOM-001).
/// </summary>
public sealed class InvalidPaymentTransitionException : PaymentException
{
    public InvalidPaymentTransitionException(Guid paymentId, PaymentStatus from, PaymentStatus to)
        : base($"Payment '{paymentId}' cannot transition from '{from}' to '{to}'.")
    {
        PaymentId = paymentId;
        From = from;
        To = to;
    }

    public Guid PaymentId { get; }
    public PaymentStatus From { get; }
    public PaymentStatus To { get; }
}

/// <summary>
/// Thrown when a money field is zero/negative where a positive amount is
/// required, or when two money fields land in a combination the domain
/// forbids (e.g. an approved amount larger than what was tendered).
/// </summary>
public sealed class InvalidPaymentAmountException : PaymentException
{
    public InvalidPaymentAmountException(string message) : base(message) { }
}
