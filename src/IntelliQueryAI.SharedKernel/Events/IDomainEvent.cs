namespace IntelliQueryAI.SharedKernel.Events;

/// <summary>
/// Marker interface for all domain events representing notable domain state transitions.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the unique identifier of the event.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Gets the timestamp when the event occurred in UTC.
    /// </summary>
    DateTime OccurredOnUtc { get; }
}

/// <summary>
/// Abstract base implementation of a domain event.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
