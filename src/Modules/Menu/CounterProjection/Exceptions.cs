namespace ALKAROS.Menu.CounterProjection;

public sealed class DailyMenuItemNotFoundException : Exception
{
    public Guid DailyMenuItemId { get; }

    public DailyMenuItemNotFoundException(Guid dailyMenuItemId)
        : base($"Daily menu item with ID '{dailyMenuItemId}' was not found.")
    {
        DailyMenuItemId = dailyMenuItemId;
    }
}

public sealed class InvalidCounterDeltaException : Exception
{
    public InvalidCounterDeltaException(string message) : base(message) { }
}

public sealed class NegativeCounterException : Exception
{
    public NegativeCounterException(string message) : base(message) { }
}
