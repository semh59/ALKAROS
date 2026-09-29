namespace ALKAROS.CustomerAccounts.CreditTerms;

/// <summary>
/// V1-RMD-440: how much a customer may owe on their account and, optionally, how many days a charge may stay
/// unpaid. A customer without a stored row has <see cref="None"/>: a limit of 0, so nothing is charged to them.
/// </summary>
public sealed record CustomerCreditTerms(
    Guid CustomerId,
    decimal CreditLimit,
    int? PaymentTermDays,
    DateTimeOffset? UpdatedAt,
    Guid? UpdatedBy)
{
    public const decimal MaxCreditLimit = 9_999_999_999.99m;
    public const int MaxPaymentTermDays = 365;

    public static CustomerCreditTerms None(Guid customerId) => new(customerId, 0m, null, null, null);

    /// <summary>Rejects values the credit_terms check constraints would refuse, before any write.</summary>
    public static void Validate(decimal creditLimit, int? paymentTermDays)
    {
        if (creditLimit < 0m || creditLimit > MaxCreditLimit || decimal.Round(creditLimit, 2) != creditLimit)
            throw new ArgumentOutOfRangeException(nameof(creditLimit), creditLimit, "Credit limit must be between 0 and 9,999,999,999.99 with at most two decimals.");
        if (paymentTermDays is < 1 or > MaxPaymentTermDays)
            throw new ArgumentOutOfRangeException(nameof(paymentTermDays), paymentTermDays, "Payment term must be between 1 and 365 days.");
    }
}
