using Microsoft.AspNetCore.SignalR;
using SeaBattle.Server.Services;
using SeaBattle.Shared;
using SeaBattle.Shared.Hub;
using Microsoft.Extensions.Logging;

namespace SeaBattle.Server.Hubs;

class BattleHub(GlobalGameStorage storage, GameService gameService, GameLogicService gameLogicService, GameLockingService gameLockingService, ILogger<BattleHub> logger) : Hub<IGameHub>
{
    private GlobalGameStorage GameStorage { get; } = storage;
    private GameService GameService { get; } = gameService;
    private GameLogicService GameLogicService { get; } = gameLogicService;
    private GameLockingService GameLockingService { get; } = gameLockingService;
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
                await GameLockingService.ExecuteWithGameLockAsync(gameState.Id, async () =>
                {
                    // Re-fetch game state within lock to ensure we have the latest state
                    var lockedGameState = GetGameState(playerId);
                    if (lockedGameState == null)
                    {
                        await Clients.Caller.Error("Game not found");
                        return;
                    }

                    // Delegate all business logic to the service (now atomic)
                    var result = await GameLogicService.ProcessShotAsync(lockedGameState, playerId, x, y);

                    if (!result.IsSuccess)
                    {
                        // Handle business logic errors
                        await Clients.Caller.Error(result.ErrorMessage ?? "Shot processing failed");
                        return;
                    }

                    if (result.IsGameOver)
                    {
                        // Handle game over
                        await HandleGameOver(lockedGameState, result.WinnerId!.Value, result.LoserId!.Value);
                    }
                    else
                    {
                        // Handle normal shot result
                        await HandleShotResult(lockedGameState, playerId, result);
                    }
                });
            }
            else
            {
                // Handle formation phase (ship placement) with locking
                await GameLockingService.ExecuteWithGameLockAsync(gameState.Id, async () =>
                {
                    // Re-fetch game state within lock to ensure we have the latest state
                    var lockedGameState = GetGameState(playerId);
                    if (lockedGameState == null)
                    {
                        await Clients.Caller.Error("Game not found");
                        return;
                    }
                    
                    await HandleFormationPhase(lockedGameState, playerId, x, y);
                });
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
            await GameLockingService.ExecuteWithGameLockAsync(gameState.Id, async () =>
            {
                // Re-fetch game state within lock to ensure we have the latest state
                var lockedGameState = GetGameState(playerId);
                if (lockedGameState == null)
                {
                    await Clients.Caller.Error("Game not found");
                    return;
                }

                // Don't allow clearing during active game
                if (lockedGameState.InProgress)
                {
                    Logger.LogWarning("Player {PlayerId} tried to clear field during active game", playerId);
                    await Clients.Caller.Error("Cannot clear field during active game");
                    return;
                }

                var playerState = lockedGameState.Players[playerId];
                playerState.ClearField();

                // Send complete updated state
                var updatedState = GameService.CreateGameStateUpdate(lockedGameState, playerId);
                await Clients.Group(playerId.ToString()).UpdateGameState(updatedState);
            });
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
}