namespace ALKAROS.CustomerData.Profiles;

/// <summary>
/// Thrown when a mutation's expected row version no longer matches the
/// stored one - another writer updated (or anonymized) the profile first.
/// Mirrors `ALKAROS.Security.DataProtectionRetention.RetentionConcurrencyException`'s
/// exact shape.
/// </summary>
public sealed class CustomerProfileConcurrencyException : Exception
{
    public Guid CustomerId { get; }

    public CustomerProfileConcurrencyException(Guid customerId)
        : base($"Customer profile {customerId} was modified by another operation; reload and retry.")
    {
        CustomerId = customerId;
    }
}
