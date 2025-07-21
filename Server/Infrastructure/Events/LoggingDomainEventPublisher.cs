using Microsoft.Extensions.Logging;
using SeaBattle.Shared.Domain.Events;

namespace SeaBattle.Server.Infrastructure.Events;

/// <summary>
/// Infrastructure implementation that publishes domain events to logging system.
/// This allows domain layer to remain pure while still providing observability.
/// </summary>
public class LoggingDomainEventPublisher : IDomainEventPublisher
{
    private readonly ILogger<LoggingDomainEventPublisher> _logger;

    public LoggingDomainEventPublisher(ILogger<LoggingDomainEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<T>(T domainEvent) where T : IDomainEvent
    {
        // Route different event types to appropriate logging levels
        switch (domainEvent)
        {
            case GameEndedEvent gameEnded:
                _logger.LogInformation("Game {GameId} ended - Winner: {WinnerId}, Loser: {LoserId}, Reason: {EndReason}",
                    gameEnded.GameId, gameEnded.WinnerId, gameEnded.LoserId, gameEnded.EndReason);
                break;

            case ShotProcessedEvent shotProcessed:
                _logger.LogDebug("Shot processed in game {GameId} by player {PlayerId} at ({X}, {Y}) - {ShotResult}",
                    shotProcessed.GameId, shotProcessed.PlayerId, shotProcessed.X, shotProcessed.Y, shotProcessed.ShotResult);
                break;

            case TurnChangedEvent turnChanged:
                _logger.LogDebug("Turn changed in game {GameId} from {PreviousPlayerId} to {NextPlayerId} - {Reason}",
                    turnChanged.GameId, turnChanged.PreviousPlayerId, turnChanged.NextPlayerId, turnChanged.Reason);
                break;

            case InvalidGameActionEvent invalidAction:
                _logger.LogWarning("Invalid action in game {GameId} by player {PlayerId}: {Action} - {Reason}",
                    invalidAction.GameId, invalidAction.PlayerId, invalidAction.Action, invalidAction.Reason);
                break;

            case GameErrorEvent gameError:
                _logger.LogError("Game error in game {GameId} for player {PlayerId}: {Operation} - {ErrorMessage}. Details: {Details}",
                    gameError.GameId, gameError.PlayerId, gameError.Operation, gameError.ErrorMessage, gameError.Details);
                break;

            default:
                _logger.LogInformation("Domain event {EventType} occurred at {OccurredAt} - {EventId}",
                    domainEvent.EventType, domainEvent.OccurredAt, domainEvent.EventId);
                break;
        }

        return Task.CompletedTask;
    }

    public async Task PublishAllAsync(IEnumerable<IDomainEvent> domainEvents)
    {
        foreach (var domainEvent in domainEvents)
        {
            await PublishAsync(domainEvent);
        }
    }
}