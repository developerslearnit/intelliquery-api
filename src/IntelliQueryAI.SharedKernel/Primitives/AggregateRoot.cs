using IntelliQueryAI.SharedKernel.Events;

namespace IntelliQueryAI.SharedKernel.Primitives;

/// <summary>
/// Base class for aggregate roots capable of raising domain events.
/// </summary>
/// <typeparam name="TId">The aggregate identifier type.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Gets the list of domain events raised by the aggregate root.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Initializes a new instance with a specified identifier.
    /// </summary>
    protected AggregateRoot(TId id) : base(id)
    {
    }

    /// <summary>
    /// Initializes a new instance for EF Core.
    /// </summary>
    protected AggregateRoot()
    {
    }

    /// <summary>
    /// Adds a domain event to the aggregate event buffer.
    /// </summary>
    /// <param name="domainEvent">The domain event to raise.</param>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears all buffered domain events from the aggregate.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

/// <summary>
/// Aggregate root defaulting to <see cref="Guid"/> as identifier.
/// </summary>
public abstract class AggregateRoot : AggregateRoot<Guid>
{
    /// <summary>
    /// Initializes a new aggregate root with a specified identifier.
    /// </summary>
    protected AggregateRoot(Guid id) : base(id)
    {
    }

    /// <summary>
    /// Initializes a new aggregate root with a new unique identifier.
    /// </summary>
    protected AggregateRoot() : base(Guid.NewGuid())
    {
    }
}
