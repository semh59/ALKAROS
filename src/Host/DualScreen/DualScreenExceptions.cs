namespace ALKAROS.Host.DualScreen;

public sealed class DualScreenConflictException : Exception
{
    public DualScreenConflictException(string message) : base(message) { }
}

public sealed class DualScreenNotFoundException : Exception
{
    public DualScreenNotFoundException(string message) : base(message) { }
}

public sealed class DualScreenUnauthorizedException : Exception
{
    public DualScreenUnauthorizedException(string message) : base(message) { }
}

public sealed class DualScreenForbiddenException : Exception
{
    public DualScreenForbiddenException(string message) : base(message) { }
}

/// <summary>A pairing step failed for a reason the cashier or the display must be able to tell apart.</summary>
public sealed class DualScreenPairingException : Exception
{
    public DualScreenPairingException(string code, int statusCode, string message) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public int StatusCode { get; }

    public const string CodeInvalid = "PAIRING_CODE_INVALID";
    public const string NotApproved = "PAIRING_NOT_APPROVED";
    public const string Expired = "PAIRING_EXPIRED";
    public const string Consumed = "PAIRING_CONSUMED";
    public const string Locked = "PAIRING_LOCKED";
}
