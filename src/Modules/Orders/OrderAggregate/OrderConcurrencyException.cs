namespace ALKAROS.Orders.OrderAggregate;

/// <summary>An order or order item was saved against a row version that is no longer current.</summary>
public sealed class OrderConcurrencyException(string message) : InvalidOperationException(message);
