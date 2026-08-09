namespace EPM.Domain.Abstractions;

/// <summary>
/// The entry point to a consistency boundary. Everything inside an aggregate is loaded,
/// changed and saved through its root, which is where the invariants are enforced.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Events raised but not yet published. The persistence layer drains this after a
    /// successful SaveChanges — raising an event is not the same as it having happened.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
