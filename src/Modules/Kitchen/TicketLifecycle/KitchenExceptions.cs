namespace ALKAROS.Kitchen.TicketLifecycle;

public sealed class KitchenTicketNotFoundException : Exception
{
    public KitchenTicketNotFoundException(Guid ticketId)
        : base($"Kitchen ticket '{ticketId}' was not found.")
    {
        TicketId = ticketId;
    }

    public Guid TicketId { get; }
}

public sealed class InvalidKitchenTransitionException : Exception
{
    public InvalidKitchenTransitionException(string message)
        : base(message)
    {
    }
}

public sealed class StaleKitchenTicketVersionException : Exception
{
    public StaleKitchenTicketVersionException(Guid ticketId, long expectedVersion, long currentVersion)
        : base($"Kitchen ticket '{ticketId}' has current row version {currentVersion}, but expected version was {expectedVersion}.")
    {
        TicketId = ticketId;
        ExpectedVersion = expectedVersion;
        CurrentVersion = currentVersion;
    }

    public Guid TicketId { get; }
    public long ExpectedVersion { get; }
    public long CurrentVersion { get; }
}
