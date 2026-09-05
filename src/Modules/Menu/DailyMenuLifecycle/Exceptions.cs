namespace ALKAROS.Menu.DailyMenuLifecycle;

public abstract class DailyMenuException : Exception
{
    protected DailyMenuException(string message) : base(message) { }
    protected DailyMenuException(string message, Exception inner) : base(message, inner) { }
}

public sealed class DailyMenuNotFoundException : DailyMenuException
{
    public Guid? DailyMenuId { get; }
    public DateOnly? BusinessDate { get; }

    public DailyMenuNotFoundException(Guid dailyMenuId)
        : base($"Daily menu with ID '{dailyMenuId}' was not found.")
    {
        DailyMenuId = dailyMenuId;
    }

    public DailyMenuNotFoundException(DateOnly businessDate)
        : base($"Daily menu for business date '{businessDate:yyyy-MM-dd}' was not found.")
    {
        BusinessDate = businessDate;
    }
}

public sealed class DailyMenuItemNotFoundException : DailyMenuException
{
    public Guid DailyMenuItemId { get; }

    public DailyMenuItemNotFoundException(Guid dailyMenuItemId)
        : base($"Daily menu item with ID '{dailyMenuItemId}' was not found.")
    {
        DailyMenuItemId = dailyMenuItemId;
    }
}

public sealed class DuplicateDailyMenuBusinessDateException : DailyMenuException
{
    public DateOnly BusinessDate { get; }

    public DuplicateDailyMenuBusinessDateException(DateOnly businessDate)
        : base($"A daily menu already exists for business date '{businessDate:yyyy-MM-dd}'. Exactly one menu per service day is allowed.")
    {
        BusinessDate = businessDate;
    }
}

public sealed class DuplicateDailyMenuItemException : DailyMenuException
{
    public Guid DailyMenuId { get; }
    public Guid ProductId { get; }

    public DuplicateDailyMenuItemException(Guid dailyMenuId, Guid productId)
        : base($"Product '{productId}' is already added to daily menu '{dailyMenuId}'.")
    {
        DailyMenuId = dailyMenuId;
        ProductId = productId;
    }
}

public sealed class DailyMenuClosedException : DailyMenuException
{
    public DailyMenuClosedException(string message) : base(message) { }
}

public sealed class InvalidDailyMenuOperationException : DailyMenuException
{
    public InvalidDailyMenuOperationException(string message) : base(message) { }
}

public sealed class InvalidDailyMenuTransitionException : DailyMenuException
{
    public InvalidDailyMenuTransitionException(string message) : base(message) { }
}
