namespace ALKAROS.ModuleComposition.Primitives;

/// <summary>
/// Marker base class for all domain events. Domain events are immutable
/// records of something that happened in the domain. Contains no business logic.
/// </summary>
public abstract class DomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}