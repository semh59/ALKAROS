namespace ALKAROS.Billing.BillFoundation;

/// <summary>A bill or bill item was saved against a row version that is no longer current.</summary>
public sealed class BillConcurrencyException(string message) : InvalidOperationException(message);
