using Microsoft.AspNetCore.SignalR;
using SeaBattle.Server.Services;
using SeaBattle.Shared;
using SeaBattle.Shared.Hub;

namespace SeaBattle.Server.Hubs;

class BattleHub(GlobalGameStorage storage, GameService gameService) : Hub<IGameHub>
{
    private GlobalGameStorage GameStorage { get; } = storage;
    private GameService GameService { get; } = gameService;

    private GameState GetGameState(Guid playerId) => GameStorage.GetGameByPlayerId(playerId) ??
                                                     throw new NullReferenceException("Game state wasn't found");

    public async Task JoinGame(Guid playerId, string playerName)
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

    public async Task PlayerReady(Guid playerId)
    {
        var gameState = GetGameState(playerId);
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

    public async Task CellClicked(Guid playerId, int x, int y)
    {
        var gameState = GetGameState(playerId);

        if (gameState.InProgress)
        {
            var playerState = gameState.Players[playerId];

            if (playerState.Shots.Contains((x, y)))
                return;

            var opponent = gameState?.Players.FirstOrDefault(p => p.Key != playerId) ??
                          throw new Exception("Couldn't find opponent's game state");
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

    public async Task ClearField(Guid playerId)
    {
        var gameState = GetGameState(playerId);
        var playerState = gameState.Players[playerId];

        playerState.ClearField();

        // Send complete updated state
        var updatedState = GameService.CreateGameStateUpdate(gameState, playerId);
        await Clients.Group(playerId.ToString()).UpdateGameState(updatedState);
    }
}