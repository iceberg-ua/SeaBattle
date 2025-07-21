namespace SeaBattle.Shared.Domain.Events;

/// <summary>
/// Base class for game-related domain events.
/// </summary>
public abstract record GameEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public abstract string EventType { get; }
    public Guid GameId { get; init; }
}

/// <summary>
/// Domain event raised when a game ends.
/// </summary>
public sealed record GameEndedEvent(Guid GameId, Guid WinnerId, Guid LoserId, string EndReason) : GameEvent
{
    public override string EventType => "GameEnded";
}

/// <summary>
/// Domain event raised when a shot is processed.
/// </summary>
public sealed record ShotProcessedEvent(
    Guid GameId, 
    Guid PlayerId, 
    int X, 
    int Y, 
    bool IsHit, 
    bool ShipDestroyed,
    string ShotResult) : GameEvent
{
    public override string EventType => "ShotProcessed";
}

/// <summary>
/// Domain event raised when a player's turn changes.
/// </summary>
public sealed record TurnChangedEvent(Guid GameId, Guid PreviousPlayerId, Guid NextPlayerId, string Reason) : GameEvent
{
    public override string EventType => "TurnChanged";
}

/// <summary>
/// Domain event raised when an invalid game action is attempted.
/// </summary>
public sealed record InvalidGameActionEvent(
    Guid GameId, 
    Guid PlayerId, 
    string Action, 
    string Reason) : GameEvent
{
    public override string EventType => "InvalidGameAction";
}

/// <summary>
/// Domain event raised when an error occurs in game processing.
/// </summary>
public sealed record GameErrorEvent(
    Guid GameId, 
    Guid? PlayerId, 
    string Operation, 
    string ErrorMessage, 
    string? Details = null) : GameEvent
{
    public override string EventType => "GameError";
}