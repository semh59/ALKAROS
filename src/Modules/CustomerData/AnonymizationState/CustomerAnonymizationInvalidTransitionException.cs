namespace ALKAROS.CustomerData.AnonymizationState;

/// <summary>Thrown when a caller asks for a transition `CustomerAnonymizationTransitions` does not allow.</summary>
public sealed class CustomerAnonymizationInvalidTransitionException : Exception
{
    public AnonymizationRequestStatus From { get; }
    public AnonymizationRequestStatus To { get; }

    public CustomerAnonymizationInvalidTransitionException(AnonymizationRequestStatus from, AnonymizationRequestStatus to)
        : base($"Cannot transition an anonymization request from {from} to {to}.")
    {
        From = from;
        To = to;
    }
}
