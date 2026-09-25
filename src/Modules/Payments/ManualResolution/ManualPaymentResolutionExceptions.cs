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

/// <summary>V1-RMD-283: the slip (receipt) number is missing or malformed.</summary>
public sealed class ManualResolutionSlipInvalidException : ManualPaymentResolutionException
{
    public ManualResolutionSlipInvalidException() : base("A valid slip number (4-32 letters, digits, - or /) is required.") { }
}

/// <summary>V1-RMD-283: this slip number already backs another live claim.</summary>
public sealed class ManualResolutionSlipReusedException : ManualPaymentResolutionException
{
    public ManualResolutionSlipReusedException(string slip) : base($"Slip number '{slip}' is already used.") { }
}

/// <summary>V1-RMD-283: the payment already has a claim waiting for approval.</summary>
public sealed class ManualResolutionAlreadyPendingException : ManualPaymentResolutionException
{
    public ManualResolutionAlreadyPendingException(Guid paymentId) : base($"Payment '{paymentId}' already has a pending claim.") { }
}

/// <summary>V1-RMD-283: the confirmation does not exist.</summary>
public sealed class ManualResolutionConfirmationNotFoundException : ManualPaymentResolutionException
{
    public ManualResolutionConfirmationNotFoundException(Guid id) : base($"Confirmation '{id}' was not found.") { }
}

/// <summary>V1-RMD-283: the confirmation was already approved or rejected.</summary>
public sealed class ManualResolutionConfirmationDecidedException : ManualPaymentResolutionException
{
    public ManualResolutionConfirmationDecidedException(Guid id, string status) : base($"Confirmation '{id}' is already {status}.") { }
}

/// <summary>V1-RMD-283: four-eyes - the person who claimed the charge cannot approve it.</summary>
public sealed class ManualResolutionSameActorException : ManualPaymentResolutionException
{
    public ManualResolutionSameActorException() : base("The person who claimed the charge cannot approve it.") { }
}
