namespace ALKAROS.Orders.OrderAggregate;

/// <summary>
/// Money and quantity rounding used by the order aggregate. Postgres money
/// columns are NUMERIC(18,2) and quantity NUMERIC(18,3); every computed value
/// rounds half-up (kuruş, V0-CMP-002 same-basket rule).
/// </summary>
internal static class OrderMath
{
    public static decimal RoundCurrency(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Tax contained in a tax-inclusive gross amount (tax first, half-up; net is gross minus this).</summary>
    public static decimal TaxIncludedIn(decimal gross, decimal taxRatePercent)
        => RoundCurrency(gross * taxRatePercent / (100m + taxRatePercent));

    public static decimal RoundQuantity(decimal value)
        => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}

/// <summary>
/// The unit rule for an ordered or refunded quantity: whole units for every product, half-unit steps (1.5) only for a
/// product that allows half portions.
/// </summary>
public static class OrderQuantityRule
{
    public static bool IsAllowed(decimal quantity, bool allowsHalfPortion)
        => quantity > 0m && quantity % (allowsHalfPortion ? 0.5m : 1m) == 0m;

    /// <exception cref="OrderQuantityNotAllowedException">The product does not allow this quantity.</exception>
    public static void Require(decimal quantity, bool allowsHalfPortion, Guid productId)
    {
        if (!IsAllowed(quantity, allowsHalfPortion))
            throw new OrderQuantityNotAllowedException(productId, quantity);
    }
}

public sealed class OrderQuantityNotAllowedException(Guid productId, decimal quantity) : ArgumentException(
    $"Quantity {quantity} is not allowed for product {productId}: whole units only, or multiples of 0.5 for a product that allows half portions.",
    nameof(quantity))
{
    public Guid ProductId { get; } = productId;
}
