using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SeaBattle.Shared;

namespace SeaBattle.Server.Services;

/// <summary>
/// Background service that periodically cleans up expired disconnections and abandoned games
/// to prevent memory leaks and ensure proper resource management.
/// </summary>
public class GameCleanupService : BackgroundService
{
    private readonly ConnectionTrackingService _connectionTrackingService;
    private readonly GameLockingService _gameLockingService;
    private readonly GlobalGameStorage _gameStorage;
    private readonly ILogger<GameCleanupService> _logger;

    // Configuration constants
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);
    private static readonly double DisconnectionGracePeriodMinutes = 2.0;
    private static readonly TimeSpan AbandonedGameThreshold = TimeSpan.FromHours(1);

    public GameCleanupService(ConnectionTrackingService connectionTrackingService,
        GameLockingService gameLockingService,
        GlobalGameStorage gameStorage,
        ILogger<GameCleanupService> logger)
    {
        _connectionTrackingService = connectionTrackingService;
        _gameLockingService = gameLockingService;
        _gameStorage = gameStorage;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GameCleanupService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredDisconnections();

                // Note: Abandoned game cleanup requires API access to enumerate all games
                // For now, the expired disconnection handling will clean up most abandoned games

                // Log cleanup statistics periodically
                LogCleanupStatistics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup process");
            }

            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when service is stopping
                break;
            }
        }

        _logger.LogInformation("GameCleanupService stopped");
    }

    /// <summary>
    /// Processes expired disconnections and removes players from games after grace period.
    /// </summary>
    private async Task ProcessExpiredDisconnections()
    {
        var expiredDisconnections = _connectionTrackingService.GetExpiredDisconnections(DisconnectionGracePeriodMinutes);

        if (expiredDisconnections.Count == 0)
            return;

        _logger.LogInformation("Processing {Count} expired disconnections", expiredDisconnections.Count);

        foreach (var disconnection in expiredDisconnections)
        {
            try
            {
                await ProcessExpiredDisconnection(disconnection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired disconnection for player {PlayerId} in game {GameId}",
                    disconnection.PlayerId, disconnection.GameId);
            }
        }
    }

    /// <summary>
    /// Processes a single expired disconnection.
    /// </summary>
    private async Task ProcessExpiredDisconnection(ConnectionTrackingService.PlayerDisconnection disconnection)
    {
        // Use game lock to ensure atomic operation
        await _gameLockingService.ExecuteWithGameLockAsync(disconnection.GameId, async () =>
        {
            // Double-check that player is still disconnected (might have reconnected)
            if (_connectionTrackingService.IsPlayerConnected(disconnection.PlayerId))
            {
                _logger.LogDebug("Player {PlayerId} reconnected, skipping expired disconnection cleanup", disconnection.PlayerId);
                _connectionTrackingService.RemovePendingDisconnection(disconnection.PlayerId);
                return;
            }

            var gameState = _gameStorage.GetGameById(disconnection.GameId);
            if (gameState == null)
            {
                _logger.LogDebug("Game {GameId} no longer exists, removing pending disconnection", disconnection.GameId);
                _connectionTrackingService.RemovePendingDisconnection(disconnection.PlayerId);
                return;
            }

            _logger.LogInformation("Processing expired disconnection for player {PlayerId} in game {GameId} after {Minutes} minutes",
                disconnection.PlayerId, disconnection.GameId,
                (DateTime.UtcNow - disconnection.DisconnectedAt).TotalMinutes);

            // Remove the player from the game
            RemovePlayerFromGameState(gameState, disconnection.PlayerId);

            // Update or remove the game based on remaining players
            HandleGameAfterPlayerRemoval(gameState);

            // Remove the pending disconnection
            _connectionTrackingService.RemovePendingDisconnection(disconnection.PlayerId);
        });
    }

    /// <summary>
    /// Removes a player from the game state.
    /// </summary>
    private void RemovePlayerFromGameState(GameState gameState, Guid playerId)
    {
        if (gameState.Players.TryGetValue(playerId, out var playerState))
        {
            gameState.Players.Remove(playerId);
            _logger.LogDebug("Removed player {PlayerId} from game {GameId}", playerId, gameState.Id);
        }
    }

    /// <summary>
    /// Handles game state after a player is removed due to disconnection.
    /// </summary>
    private void HandleGameAfterPlayerRemoval(GameState gameState)
    {
        if (gameState.Players.Count == 0)
        {
            // No players left - remove the game completely
            _logger.LogInformation("Removing empty game {GameId} after all players disconnected", gameState.Id);
            _gameStorage.RemoveGame(gameState.Id);
            _gameLockingService.CleanupGameLock(gameState.Id);
        }
        else if (gameState.Players.Count == 1)
        {
            // Only one player left - end the game or mark as waiting
            var remainingPlayer = gameState.Players.First();
            _logger.LogInformation("Game {GameId} now has only one player {PlayerId} remaining",
                gameState.Id, remainingPlayer.Key);

            // For now, remove single-player games. In the future, could implement waiting for new opponent
            _gameStorage.RemoveGame(gameState.Id);
            _gameLockingService.CleanupGameLock(gameState.Id);
        }
        // If 2+ players remain, game continues normally
    }


    /// <summary>
    /// Logs cleanup statistics for monitoring.
    /// </summary>
    private void LogCleanupStatistics()
    {
        var stats = _connectionTrackingService.GetStats();

        _logger.LogDebug("Cleanup stats - Active connections: {Connections}, Connected players: {Players}",
            stats.TotalConnections, stats.ConnectedPlayers);
    }
}