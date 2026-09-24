namespace ALKAROS.Payments.ManualResolution;

/// <summary>Base exception for manual payment resolution errors (V1-RMD-264).</summary>
public abstract class ManualPaymentResolutionException : Exception
{
    protected ManualPaymentResolutionException(string message) : base(message) { }
}

/// <summary>Thrown when the Payment to resolve does not exist.</summary>
public sealed class ManualResolutionPaymentNotFoundException : ManualPaymentResolutionException
{
    public ManualResolutionPaymentNotFoundException(Guid paymentId)
        : base($"Payment '{paymentId}' was not found.")
    {
        PaymentId = paymentId;
    }

    public Guid PaymentId { get; }
}

/// <summary>
/// Thrown when the Payment is not in a state a manager may resolve by hand.
/// Only Unknown and ReconciliationRequired qualify: a Pending payment is a
/// tender still in flight and must never be overridden from outside it.
/// </summary>
public sealed class ManualResolutionNotResolvableException : ManualPaymentResolutionException
{
    public ManualResolutionNotResolvableException(Guid paymentId, string status)
        : base($"Payment '{paymentId}' is '{status}' and cannot be resolved manually.")
    {
        PaymentId = paymentId;
        Status = status;
    }

    public Guid PaymentId { get; }
    public string Status { get; }
}

/// <summary>Thrown when the manager supplies no (or an over-long) reason.</summary>
public sealed class ManualResolutionReasonInvalidException : ManualPaymentResolutionException
{
    public ManualResolutionReasonInvalidException(string message) : base(message) { }
}
