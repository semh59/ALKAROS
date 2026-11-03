namespace ALKAROS.Cash.Contracts;

/// <summary>A cash session was saved against a row version that is no longer current.</summary>
public sealed class CashSessionConcurrencyException(string message) : InvalidOperationException(message);
