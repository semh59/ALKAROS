using ALKAROS.Cash.Contracts;

namespace ALKAROS.Cash.TransactionLedger;

/// <summary>Base exception for CashTransaction domain errors (V13-CSH-002).</summary>
public abstract class CashTransactionException : Exception
{
    protected CashTransactionException(string message) : base(message) { }
}

/// <summary>Thrown when a transaction amount is zero or negative.</summary>
public sealed class InvalidCashTransactionAmountException : CashTransactionException
{
    public InvalidCashTransactionAmountException(decimal amount)
        : base($"Cash transaction amount '{amount}' must be greater than zero.")
    {
        Amount = amount;
    }

    public decimal Amount { get; }
}

/// <summary>
/// Thrown when a transaction type with a fixed direction (Opening/Sale/
/// CashIn are always In; CashOut/Refund are always Out) is given the
/// wrong one.
/// </summary>
public sealed class InvalidCashTransactionDirectionException : CashTransactionException
{
    public InvalidCashTransactionDirectionException(
        CashTransactionType type, CashTransactionDirection given, CashTransactionDirection expected)
        : base($"Cash transaction type '{type}' must have direction '{expected}', not '{given}'.")
    {
        Type = type;
        Given = given;
        Expected = expected;
    }

    public CashTransactionType Type { get; }
    public CashTransactionDirection Given { get; }
    public CashTransactionDirection Expected { get; }
}

/// <summary>Thrown when a Sale/Refund entry does not name the Payment it moves cash for.</summary>
public sealed class MissingRelatedPaymentException : CashTransactionException
{
    public MissingRelatedPaymentException(CashTransactionType type)
        : base($"Cash transaction type '{type}' must carry a related payment id.")
    {
        Type = type;
    }

    public CashTransactionType Type { get; }
}

/// <summary>Thrown when a non-Sale/Refund entry carries a related payment id it has no use for.</summary>
public sealed class UnexpectedRelatedPaymentException : CashTransactionException
{
    public UnexpectedRelatedPaymentException(CashTransactionType type)
        : base($"Cash transaction type '{type}' must not carry a related payment id.")
    {
        Type = type;
    }

    public CashTransactionType Type { get; }
}

/// <summary>Thrown when a CountAdjustment entry does not explain why it was made.</summary>
public sealed class MissingCountAdjustmentReasonException : CashTransactionException
{
    public MissingCountAdjustmentReasonException()
        : base("A CountAdjustment entry must state its reason in Notes — a recount correction is never a bare unexplained number.")
    {
    }
}
