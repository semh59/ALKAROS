namespace ALKAROS.CustomerData.Profiles;

/// <summary>
/// V14-CST-001's own In scope note: an anonymized customer record cannot
/// have an e-Fatura issued against it - the guard `V14-INV-002` will need
/// at the point it builds an invoice draft from a customer reference.
/// </summary>
public static class CustomerProfileRetention
{
    public static void ThrowIfCannotInvoice(CustomerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Anonymized)
            throw new CustomerProfileAnonymizedException(profile.CustomerId);
    }
}
