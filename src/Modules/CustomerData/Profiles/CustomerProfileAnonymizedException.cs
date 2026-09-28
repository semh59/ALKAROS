namespace ALKAROS.CustomerData.Profiles;

/// <summary>
/// V14-CST-001's own In scope note: an anonymized customer record cannot
/// have an e-Fatura issued against it - a guard `V14-INV-002` needs at the
/// point it builds an invoice draft from a customer reference.
/// </summary>
public sealed class CustomerProfileAnonymizedException : Exception
{
    public Guid CustomerId { get; }

    public CustomerProfileAnonymizedException(Guid customerId)
        : base($"Customer {customerId} is anonymized; an invoice cannot be issued against it.")
    {
        CustomerId = customerId;
    }
}
