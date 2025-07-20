using Microsoft.AspNetCore.SignalR;
using SeaBattle.Server.Services;
using SeaBattle.Shared;
using SeaBattle.Shared.Hub;
using Microsoft.Extensions.Logging;

namespace SeaBattle.Server.Hubs;

class BattleHub(GlobalGameStorage storage, GameService gameService, ILogger<BattleHub> logger) : Hub<IGameHub>
{
    private GlobalGameStorage GameStorage { get; } = storage;
    private GameService GameService { get; } = gameService;
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
            if (playerId == Guid.Empty)
            {
                Logger.LogWarning("Invalid player ID provided: {PlayerId}", playerId);
                await Clients.Caller.Error("Invalid player ID");
                return;
            }

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

            // Validate coordinates
            if (x < 0 || x >= gameState.Size || y < 0 || y >= gameState.Size)
            {
                Logger.LogWarning("Invalid coordinates ({X}, {Y}) for player {PlayerId}", x, y, playerId);
                await Clients.Caller.Error("Invalid coordinates");
                return;
            }

            if (gameState.InProgress)
            {
                var playerState = gameState.Players[playerId];

                // Check if it's the player's turn
                if (playerState.State != PlayerStateEnum.InTurn)
                {
                    Logger.LogWarning("Player {PlayerId} tried to shoot when not their turn", playerId);
                    await Clients.Caller.Error("Not your turn");
                    return;
                }

                if (playerState.Shots.Contains((x, y)))
                    return;

                var opponent = gameState.Players.FirstOrDefault(p => p.Key != playerId);
                if (opponent.Key == Guid.Empty)
                {
                    Logger.LogError("Opponent not found for player {PlayerId} in game {GameId}", playerId, gameState.Id);
                    await Clients.Caller.Error("Opponent not found");
                    return;
                }

                var opponentState = opponent.Value;
                var shotResult = opponentState.CheckShotResult(x, y);

                if (shotResult != null)
                {
                    playerState.Shots.Push((x, y));

                    // Check for game over
                    if (opponentState.Fleet.Ships.Count == 0)
                    {
                        gameState.Stage = GameStageEnum.GameOver;
                        playerState.SetWon();
                        opponentState.SetLost();

                        // Send final game states
                        var winnerState = GameService.CreateGameStateUpdate(gameState, playerId);
                        var loserState = GameService.CreateGameStateUpdate(gameState, opponent.Key);
                        
                        await Clients.Group(playerId.ToString()).UpdateGameState(winnerState);
                        await Clients.Group(opponent.Key.ToString()).UpdateGameState(loserState);
                        
                        await Clients.Group(playerId.ToString()).GameOver(win: true);
                        await Clients.Group(opponent.Key.ToString()).GameOver(win: false);

                        // Clean up the game
                        GameStorage.RemoveGame(gameState.Id);
                        return;
                    }
                    else if (!shotResult.Any(s => s.Value == CellState.hit))
                    {
                        // Miss - switch turns
                        opponentState.SetInTurn();
                        playerState.SetWaitingForTurn();
                    }
                    // If hit but not game over, current player continues

                    // Send updated states to both players
                    var currentPlayerState = GameService.CreateGameStateUpdate(gameState, playerId);
                    var opponentPlayerState = GameService.CreateGameStateUpdate(gameState, opponent.Key);
                    
                    await Clients.Group(playerId.ToString()).UpdateGameState(currentPlayerState);
                    await Clients.Group(opponent.Key.ToString()).UpdateGameState(opponentPlayerState);
                }
            }
            else
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
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in ClearField for player {PlayerId}", playerId);
            await Clients.Caller.Error("Failed to clear field");
        }
    }
}