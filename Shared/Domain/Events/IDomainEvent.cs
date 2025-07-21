namespace SeaBattle.Shared.Domain.Events;

/// <summary>
/// Base interface for all domain events.
/// Domain events represent business-significant occurrences within the domain.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// The unique identifier of this domain event.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// The timestamp when this event occurred.
    /// </summary>
    DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// The type of the event, used for routing and logging.
    /// </summary>
    string EventType { get; }
}