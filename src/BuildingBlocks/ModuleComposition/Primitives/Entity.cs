namespace ALKAROS.ModuleComposition.Primitives;

/// <summary>
/// Base class for all domain entities. Provides identity equality and
/// domain-event collection. Contains no business logic.
/// </summary>
public abstract class Entity
{
    private readonly List<DomainEvent> _events = new();

    public Guid Id { get; protected set; }

    public IReadOnlyList<DomainEvent> DomainEvents => _events;

    protected void Raise(DomainEvent @event) => _events.Add(@event);

    public void ClearDomainEvents() => _events.Clear();

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (GetType() != other.GetType())
            return false;
        return Id == other.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity? left, Entity? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity? left, Entity? right)
        => !(left == right);
}