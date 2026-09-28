namespace ALKAROS.CustomerData.Profiles;

/// <summary>Thrown by a mutation targeting a customer id that does not exist.</summary>
public sealed class CustomerProfileNotFoundException : Exception
{
    public Guid CustomerId { get; }

    public CustomerProfileNotFoundException(Guid customerId)
        : base($"Customer profile {customerId} was not found.")
    {
        CustomerId = customerId;
    }
}
