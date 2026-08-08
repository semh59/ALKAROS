namespace ALKAROS.Identity.DeviceSessions;

public sealed class InvalidSessionTokenException : Exception
{
    public InvalidSessionTokenException(string message) : base(message)
    {
    }
}

public sealed class DeviceSessionRevokedException : Exception
{
    public DeviceSessionRevokedException(string message) : base(message)
    {
    }
}

public sealed class DeviceSessionExpiredException : Exception
{
    public DeviceSessionExpiredException(string message) : base(message)
    {
    }
}