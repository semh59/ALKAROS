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
