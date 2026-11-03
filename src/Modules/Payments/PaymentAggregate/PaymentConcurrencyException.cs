namespace ALKAROS.Payments.PaymentAggregate;

/// <summary>A payment, refund intent or manual card confirmation was saved against a row version that is no longer current.</summary>
public sealed class PaymentConcurrencyException(string message) : InvalidOperationException(message);
