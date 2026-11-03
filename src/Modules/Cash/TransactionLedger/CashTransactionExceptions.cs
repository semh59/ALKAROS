using ALKAROS.Cash.Contracts;

namespace ALKAROS.Cash.TransactionLedger;

/// <summary>Base exception for CashTransaction domain errors (V13-CSH-002).</summary>
public abstract class CashTransactionException : Exception
{
    protected CashTransactionException(string message) : base(message) { }
}

/// <summary>Thrown when a transaction amount is not positive, has more than two decimals, or exceeds the ledger limit.</summary>
public sealed class InvalidCashTransactionAmountException : CashTransactionException
{
    public InvalidCashTransactionAmountException(decimal amount)
        : base($"Cash transaction amount '{amount}' must be greater than zero, have at most two decimals and not exceed the ledger limit.")
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

/// <summary>Thrown when a manual cash-in or cash-out has no reason, or one longer than the limit.</summary>
public sealed class InvalidCashMovementReasonException : CashTransactionException
{
    public InvalidCashMovementReasonException()
        : base($"A manual cash movement must state a reason of 1 to {CashTransaction.MaxReasonLength} characters.")
    {
    }
}

/// <summary>Thrown when a cash-out is more than the cash the drawer holds.</summary>
public sealed class CashOutExceedsDrawerException : CashTransactionException
{
    public CashOutExceedsDrawerException(decimal requested, decimal available)
        : base($"Cash-out of {requested} is more than the {available} the drawer holds.")
    {
        Requested = requested;
        Available = available;
    }

    public decimal Requested { get; }
    public decimal Available { get; }
}

/// <summary>Thrown when the request does not exist on the session named in the route.</summary>
public sealed class CashMovementRequestNotFoundException : CashTransactionException
{
    public CashMovementRequestNotFoundException(Guid requestId)
        : base($"Cash movement request {requestId} was not found.")
    {
    }
}

/// <summary>Thrown when a request that is no longer open is decided again.</summary>
public sealed class CashMovementRequestStateException : CashTransactionException
{
    public CashMovementRequestStateException(Guid requestId, string status)
        : base($"Cash movement request {requestId} is {status}.")
    {
    }
}

/// <summary>Thrown when a user approves their own cash-out while the self-approval setting is off.</summary>
public sealed class CashMovementSelfApprovalException : CashTransactionException
{
    public CashMovementSelfApprovalException()
        : base("The person who asked for a cash-out cannot approve it.")
    {
    }
}

/// <summary>Thrown when a session with open cash-out requests is closed.</summary>
public sealed class PendingCashMovementRequestsException : CashTransactionException
{
    public PendingCashMovementRequestsException(Guid cashSessionId)
        : base($"Cash session {cashSessionId} still has cash-out requests waiting for a decision.")
    {
    }
}

/// <summary>Thrown when an idempotency key already belongs to a different cash movement.</summary>
public sealed class CashMovementKeyReusedException : CashTransactionException
{
    public CashMovementKeyReusedException()
        : base("The idempotency key was already used for a different cash movement.")
    {
    }
}
