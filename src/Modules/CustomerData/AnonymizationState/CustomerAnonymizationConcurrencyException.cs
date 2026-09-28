namespace ALKAROS.CustomerData.AnonymizationState;

/// <summary>Thrown when a mutation's expected row version no longer matches the stored one.</summary>
public sealed class CustomerAnonymizationConcurrencyException : Exception
{
    public Guid RequestId { get; }

    public CustomerAnonymizationConcurrencyException(Guid requestId)
        : base($"Anonymization request {requestId} was modified by another operation; reload and retry.")
    {
        RequestId = requestId;
    }
}
