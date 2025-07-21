namespace SeaBattle.Shared.Domain.Events;

/// <summary>
/// Domain abstraction for publishing domain events.
/// Allows domain layer to communicate without depending on infrastructure.
/// </summary>
public interface IDomainEventPublisher
{
    /// <summary>
    /// Publishes a domain event to be handled by infrastructure or application layer.
    /// </summary>
    /// <param name="domainEvent">The event to publish</param>
    Task PublishAsync<T>(T domainEvent) where T : IDomainEvent;

    /// <summary>
    /// Publishes multiple domain events.
    /// </summary>
    /// <param name="domainEvents">The events to publish</param>
    Task PublishAllAsync(IEnumerable<IDomainEvent> domainEvents);
}