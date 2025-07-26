using Microsoft.AspNetCore.SignalR;
using SeaBattle.Server.Services;
using SeaBattle.Shared;
using SeaBattle.Shared.Hub;
using Microsoft.Extensions.Logging;

namespace SeaBattle.Server.Hubs;

public class BattleHub(GlobalGameStorage storage, GameService gameService, GameLogicService gameLogicService, GameLockingService gameLockingService, ConnectionTrackingService connectionTracking, ILogger<BattleHub> logger) : Hub<IGameHub>
{
    private GlobalGameStorage GameStorage { get; } = storage;
    private GameService GameService { get; } = gameService;
    private GameLogicService GameLogicService { get; } = gameLogicService;
    private GameLockingService GameLockingService { get; } = gameLockingService;
    private ConnectionTrackingService ConnectionTracking { get; } = connectionTracking;
    private ILogger<BattleHub> Logger { get; } = logger;

    private GameState? GetGameState(Guid playerId)
    {
        try
        {
            return GameStorage.GetGameByPlayerId(playerId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving game state for player {PlayerId}", playerId);
            return null;
        }
    }

    public async Task JoinGame(Guid playerId, string playerName)
    {
        try
        {
            var gameState = GameStorage.GetGameByPlayerId(playerId);

            if (gameState == null && !string.IsNullOrEmpty(playerName))
            {
                gameState = GameStorage.CreateGame();
                var playerState = GameService.AddPlayer(gameState, playerName);
                playerId = playerState.PlayerId;
            }

            if (gameState is not null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, gameState.Id.ToString());
                await Groups.AddToGroupAsync(Context.ConnectionId, playerId.ToString());
                
                // Track the connection
                ConnectionTracking.RegisterConnection(Context.ConnectionId, playerId, gameState.Id);
            }

            await Clients.Caller.JoinedGame(GameService.GetClientGameState(gameState, playerId, includeEnemyField: gameState?.InProgress ?? false));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in JoinGame for player {PlayerId}", playerId);
            await Clients.Caller.Error("Failed to join game");
        }
    }

    public async Task PlayerReady(Guid playerId)
    {
        try
        {
            var gameState = GetGameState(playerId);
            if (gameState == null)
            {
                Logger.LogWarning("Game state not found for player {PlayerId}", playerId);
                await Clients.Caller.Error("Game not found");
                return;
            }

            if (!gameState.Players.ContainsKey(playerId))
            {
                Logger.LogWarning("Player {PlayerId} not found in game {GameId}", playerId, gameState.Id);
                await Clients.Caller.Error("Player not found in game");
                return;
            }

            // Use game-level locking to prevent race conditions during player ready processing
            await GameLockingService.ExecuteWithGameLockAsync(gameState.Id, () => ProcessPlayerReadyAtomically(playerId));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in PlayerReady for player {PlayerId}", playerId);
            await Clients.Caller.Error("Failed to set player ready");
        }
    }

    public async Task CellClicked(Guid playerId, int x, int y)
    {
        try
        {
            // Basic input validation
            var gameState = GetGameState(playerId);
            if (gameState == null)
            {
                Logger.LogWarning("Game state not found for player {PlayerId}", playerId);
                await Clients.Caller.Error("Game not found");
                return;
            }

            if (!gameState.Players.ContainsKey(playerId))
            {
                Logger.LogWarning("Player {PlayerId} not found in game {GameId}", playerId, gameState.Id);
                await Clients.Caller.Error("Player not found in game");
                return;
            }

            if (gameState.InProgress)
            {
                // Use game-level locking to prevent race conditions during shot processing
                await GameLockingService.ExecuteWithGameLockAsync(gameState.Id, () => ProcessShotAtomically(playerId, x, y));
            }
            else
            {
                // Handle formation phase (ship placement) with locking
                await GameLockingService.ExecuteWithGameLockAsync(gameState.Id, () => ProcessFormationPhaseAtomically(playerId, x, y));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in CellClicked for player {PlayerId} at ({X}, {Y})", playerId, x, y);
            await Clients.Caller.Error("Failed to process cell click");
        }
    }

    public async Task ClearField(Guid playerId)
    {
        try
        {
            var gameState = GetGameState(playerId);
            if (gameState == null)
            {
                Logger.LogWarning("Game state not found for player {PlayerId}", playerId);
                await Clients.Caller.Error("Game not found");
                return;
            }

            if (!gameState.Players.ContainsKey(playerId))
            {
                Logger.LogWarning("Player {PlayerId} not found in game {GameId}", playerId, gameState.Id);
                await Clients.Caller.Error("Player not found in game");
                return;
            }

            // Use game-level locking to prevent race conditions during field clearing
            await GameLockingService.ExecuteWithGameLockAsync(gameState.Id, () => ProcessClearFieldAtomically(playerId));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in ClearField for player {PlayerId}", playerId);
            await Clients.Caller.Error("Failed to clear field");
        }
    }

    /// <summary>
    /// Handles communication for game over scenario.
    /// </summary>
    private async Task HandleGameOver(GameState gameState, Guid winnerId, Guid loserId)
    {
        // Send final game states
        var winnerState = GameService.CreateGameStateUpdate(gameState, winnerId);
        var loserState = GameService.CreateGameStateUpdate(gameState, loserId);
        
        await Clients.Group(winnerId.ToString()).UpdateGameState(winnerState);
        await Clients.Group(loserId.ToString()).UpdateGameState(loserState);
        
        // Send game over notifications
        await Clients.Group(winnerId.ToString()).GameOver(win: true);
        await Clients.Group(loserId.ToString()).GameOver(win: false);

        // Clean up the game and its lock
        GameStorage.RemoveGame(gameState.Id);
        GameLockingService.CleanupGameLock(gameState.Id);
    }

    /// <summary>
    /// Handles communication for normal shot results.
    /// </summary>
    private async Task HandleShotResult(GameState gameState, Guid currentPlayerId, ShotProcessingResult result)
    {
        // Find opponent for state updates
        var opponentId = gameState.Players.Keys.First(id => id != currentPlayerId);

        // Send updated states to both players
        var currentPlayerState = GameService.CreateGameStateUpdate(gameState, currentPlayerId);
        var opponentPlayerState = GameService.CreateGameStateUpdate(gameState, opponentId);
        
        await Clients.Group(currentPlayerId.ToString()).UpdateGameState(currentPlayerState);
        await Clients.Group(opponentId.ToString()).UpdateGameState(opponentPlayerState);
    }

    /// <summary>
    /// Handles communication for formation phase (ship placement).
    /// </summary>
    private async Task HandleFormationPhase(GameState gameState, Guid playerId, int x, int y)
    {
        var playerState = gameState.Players[playerId];

        if (playerState.TryToUpdateState(x, y))
        {
            // Send complete updated state
            var updatedState = GameService.CreateGameStateUpdate(gameState, playerId);
            await Clients.Group(playerId.ToString()).UpdateGameState(updatedState);
        }
    }

    // ========================================
    // Atomic Operations (Protected by Game Locks)
    // ========================================

    /// <summary>
    /// Processes player ready state atomically within a game lock.
    /// Handles game start logic and turn assignment.
    /// </summary>
    private async Task ProcessPlayerReadyAtomically(Guid playerId)
    {
        // Re-fetch game state within lock to ensure we have the latest state
        var gameState = GetGameState(playerId);
        if (gameState == null)
        {
            await Clients.Caller.Error("Game not found");
            return;
        }

        GameService.SetPlayerReady(gameState, playerId);

        // Send updated state to the ready player
        var readyPlayerState = GameService.CreateGameStateUpdate(gameState, playerId);
        await Clients.Group(playerId.ToString()).UpdateGameState(readyPlayerState);

        if (gameState.InProgress)
        {
            await Clients.Group(gameState.Id.ToString()).GameStarted();

            // Set random player to start
            var randomPlayer = gameState.Players.Keys.ElementAt(Random.Shared.Next(0, gameState.Players.Count));
            var firstPlayer = gameState.Players[randomPlayer];
            var secondPlayer = gameState.Players.Values.First(p => p.PlayerId != randomPlayer);
            
            firstPlayer.SetInTurn();
            secondPlayer.SetWaitingForTurn();

            // Send complete state updates to both players
            var firstPlayerState = GameService.CreateGameStateUpdate(gameState, randomPlayer);
            var secondPlayerState = GameService.CreateGameStateUpdate(gameState, secondPlayer.PlayerId);
            
            await Clients.Group(randomPlayer.ToString()).UpdateGameState(firstPlayerState);
            await Clients.Group(secondPlayer.PlayerId.ToString()).UpdateGameState(secondPlayerState);
        }
    }

    /// <summary>
    /// Processes shot atomically within a game lock.
    /// Handles shot validation, processing, and result communication.
    /// </summary>
    private async Task ProcessShotAtomically(Guid playerId, int x, int y)
    {
        // Re-fetch game state within lock to ensure we have the latest state
        var gameState = GetGameState(playerId);
        if (gameState == null)
        {
            await Clients.Caller.Error("Game not found");
            return;
        }

        // Delegate all business logic to the service (now atomic)
        var result = await GameLogicService.ProcessShotAsync(gameState, playerId, x, y);

        if (!result.IsSuccess)
        {
            // Handle business logic errors
            await Clients.Caller.Error(result.ErrorMessage ?? "Shot processing failed");
            return;
        }

        if (result.IsGameOver)
        {
            // Handle game over
            await HandleGameOver(gameState, result.WinnerId!.Value, result.LoserId!.Value);
        }
        else
        {
            // Handle normal shot result
            await HandleShotResult(gameState, playerId, result);
        }
    }

    /// <summary>
    /// Processes formation phase (ship placement) atomically within a game lock.
    /// </summary>
    private async Task ProcessFormationPhaseAtomically(Guid playerId, int x, int y)
    {
        // Re-fetch game state within lock to ensure we have the latest state
        var gameState = GetGameState(playerId);
        if (gameState == null)
        {
            await Clients.Caller.Error("Game not found");
            return;
        }
        
        await HandleFormationPhase(gameState, playerId, x, y);
    }

    /// <summary>
    /// Processes field clearing atomically within a game lock.
    /// </summary>
    private async Task ProcessClearFieldAtomically(Guid playerId)
    {
        // Re-fetch game state within lock to ensure we have the latest state
        var gameState = GetGameState(playerId);
        if (gameState == null)
        {
            await Clients.Caller.Error("Game not found");
            return;
        }

        // Don't allow clearing during active game
        if (gameState.InProgress)
        {
            Logger.LogWarning("Player {PlayerId} tried to clear field during active game", playerId);
            await Clients.Caller.Error("Cannot clear field during active game");
            return;
        }

        var playerState = gameState.Players[playerId];
        playerState.ClearField();

        // Send complete updated state
        var updatedState = GameService.CreateGameStateUpdate(gameState, playerId);
        await Clients.Group(playerId.ToString()).UpdateGameState(updatedState);
    }

    public async Task ReconnectToGame(Guid playerId)
    {
        try
        {
            var gameState = GetGameState(playerId);
            if (gameState == null)
            {
                Logger.LogInformation("No active game found for player {PlayerId}, creating new game", playerId);
                // No existing game found, create a new one
                await JoinGame(playerId, "");
                return;
            }

            if (!gameState.Players.ContainsKey(playerId))
            {
                Logger.LogInformation("Player {PlayerId} not found in game {GameId}, creating new game", playerId, gameState.Id);
                // Player not in the found game, create a new one
                await JoinGame(playerId, "");
                return;
            }

            Logger.LogInformation("Player {PlayerId} reconnecting to game {GameId}", playerId, gameState.Id);

            // Re-add to groups and update connection tracking
            await Groups.AddToGroupAsync(Context.ConnectionId, gameState.Id.ToString());
            await Groups.AddToGroupAsync(Context.ConnectionId, playerId.ToString());
            ConnectionTracking.RegisterConnection(Context.ConnectionId, playerId, gameState.Id);

            // Clear any pending disconnection since player successfully reconnected
            if (ConnectionTracking.HasPendingDisconnection(playerId))
            {
                ConnectionTracking.RemovePendingDisconnection(playerId);
                Logger.LogInformation("Player {PlayerId} reconnected within grace period, cancelling pending disconnection", playerId);
            }

            // Send current game state
            var clientGameState = GameService.GetClientGameState(gameState, playerId, includeEnemyField: gameState.InProgress);
            await Clients.Caller.JoinedGame(clientGameState);

            Logger.LogInformation("Player {PlayerId} successfully reconnected to game {GameId}", playerId, gameState.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in ReconnectToGame for player {PlayerId}", playerId);
            await Clients.Caller.Error("Failed to reconnect to game");
        }
    }

    /// <summary>
    /// Handles player disconnection, cleaning up connections and managing game state.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            Logger.LogInformation("Connection {ConnectionId} disconnected", Context.ConnectionId);
            
            // Remove the connection and get player info
            var connection = ConnectionTracking.RemoveConnection(Context.ConnectionId);
            if (connection == null)
            {
                Logger.LogWarning("No connection info found for disconnected connection {ConnectionId}", Context.ConnectionId);
                await base.OnDisconnectedAsync(exception);
                return;
            }

            Logger.LogInformation("Player {PlayerId} disconnected from connection {ConnectionId}", connection.PlayerId, Context.ConnectionId);

            // Check if player has other active connections
            if (ConnectionTracking.IsPlayerConnected(connection.PlayerId))
            {
                Logger.LogInformation("Player {PlayerId} has other active connections, no further action needed", connection.PlayerId);
                await base.OnDisconnectedAsync(exception);
                return;
            }

            // Player is completely disconnected, handle game cleanup if in a game
            if (connection.GameId.HasValue)
            {
                await HandlePlayerDisconnectedFromGame(connection.PlayerId, connection.GameId.Value);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling disconnection for connection {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Handles player disconnection from an active game.
    /// </summary>
    private async Task HandlePlayerDisconnectedFromGame(Guid playerId, Guid gameId)
    {
        try
        {
            // Use game-level locking to prevent race conditions during disconnect processing
            await GameLockingService.ExecuteWithGameLockAsync(gameId, () => ProcessPlayerDisconnectAtomically(playerId, gameId));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling player disconnect for player {PlayerId} in game {GameId}", playerId, gameId);
        }
    }

    /// <summary>
    /// Processes player disconnect atomically within a game lock.
    /// </summary>
    private async Task ProcessPlayerDisconnectAtomically(Guid playerId, Guid gameId)
    {
        var gameState = GameStorage.GetGameById(gameId);
        if (gameState == null)
        {
            Logger.LogWarning("Game {GameId} not found during disconnect processing for player {PlayerId}", gameId, playerId);
            return;
        }

        if (!gameState.Players.ContainsKey(playerId))
        {
            Logger.LogWarning("Player {PlayerId} not found in game {GameId} during disconnect processing", playerId, gameId);
            return;
        }

        Logger.LogInformation("Processing disconnect for player {PlayerId} in game {GameId}, InProgress: {InProgress}", 
            playerId, gameId, gameState.InProgress);

        if (gameState.InProgress)
        {
            // Game is active - add to pending disconnections for grace period
            Logger.LogInformation("Player {PlayerId} disconnected from active game {GameId}, starting grace period for reconnection", playerId, gameId);
            ConnectionTracking.AddPendingDisconnection(playerId, gameId);
        }
        else
        {
            // Game is in setup phase - remove player and clean up if needed
            await HandleSetupPhaseDisconnect(gameState, playerId);
        }
    }

    /// <summary>
    /// Handles forfeit when a player disconnects during an active game.
    /// </summary>
    private async Task HandleGameForfeit(GameState gameState, Guid disconnectedPlayerId)
    {
        try
        {
            // Find the opponent
            var opponentId = gameState.Players.Keys.FirstOrDefault(id => id != disconnectedPlayerId);
            if (opponentId == Guid.Empty)
            {
                Logger.LogWarning("No opponent found for disconnected player {PlayerId} in game {GameId}", disconnectedPlayerId, gameState.Id);
                // Clean up single-player game
                GameStorage.RemoveGame(gameState.Id);
                GameLockingService.CleanupGameLock(gameState.Id);
                return;
            }

            Logger.LogInformation("Player {PlayerId} forfeited due to disconnect, opponent {OpponentId} wins", disconnectedPlayerId, opponentId);

            // Handle game over with forfeit
            await HandleGameOver(gameState, opponentId, disconnectedPlayerId);

            // Send forfeit notification to opponent if connected
            if (ConnectionTracking.IsPlayerConnected(opponentId))
            {
                await Clients.Group(opponentId.ToString()).PlayerDisconnected(disconnectedPlayerId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling forfeit for player {PlayerId} in game {GameId}", disconnectedPlayerId, gameState.Id);
        }
    }

    /// <summary>
    /// Handles disconnect during setup phase.
    /// </summary>
    private async Task HandleSetupPhaseDisconnect(GameState gameState, Guid disconnectedPlayerId)
    {
        try
        {
            // Remove the disconnected player
            gameState.Players.Remove(disconnectedPlayerId);

            if (gameState.Players.Count == 0)
            {
                // No players left - remove the game
                Logger.LogInformation("Removing empty game {GameId} after all players disconnected", gameState.Id);
                GameStorage.RemoveGame(gameState.Id);
                GameLockingService.CleanupGameLock(gameState.Id);
            }
            else
            {
                // Notify remaining players
                var remainingPlayers = gameState.Players.Keys.ToList();
                foreach (var playerId in remainingPlayers)
                {
                    if (ConnectionTracking.IsPlayerConnected(playerId))
                    {
                        var updatedState = GameService.CreateGameStateUpdate(gameState, playerId);
                        await Clients.Group(playerId.ToString()).UpdateGameState(updatedState);
                        await Clients.Group(playerId.ToString()).PlayerDisconnected(disconnectedPlayerId);
                    }
                }

                Logger.LogInformation("Player {PlayerId} removed from game {GameId}, {RemainingCount} players remaining", 
                    disconnectedPlayerId, gameState.Id, gameState.Players.Count);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling setup phase disconnect for player {PlayerId} in game {GameId}", disconnectedPlayerId, gameState.Id);
        }
    }
}