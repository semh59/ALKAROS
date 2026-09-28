namespace ALKAROS.CustomerData.AnonymizationState;

public sealed class CustomerAnonymizationRequestNotFoundException : Exception
{
    public Guid RequestId { get; }

    public CustomerAnonymizationRequestNotFoundException(Guid requestId)
        : base($"Anonymization request {requestId} was not found.")
    {
        RequestId = requestId;
    }
}
