using Microsoft.AspNetCore.SignalR;
using SeaBattle.Shared;
using SeaBattle.Shared.Hub;

namespace SeaBattle.Server.Hubs;

class BattleHub(GlobalGameStorage storage) : Hub<IGameHub>
{
    private GlobalGameStorage GameStorage { get; } = storage;

    private GameState GetGameState(Guid playerId) => GameStorage.GetGameByPlayerId(playerId) ??
                                                     throw new NullReferenceException("Game state wasn't found");

    public async Task JoinGame(Guid playerId, string playerName)
    {
        var gameState = GameStorage.GetGameByPlayerId(playerId);

        if (gameState == null && !string.IsNullOrEmpty(playerName))
        {
            gameState = GameStorage.CreateGame();
            var playerState = gameState.AddPlayer(playerName);
            playerId = playerState.PlayerId;
        }
        
        if (gameState is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, gameState.Id.ToString());
            await Groups.AddToGroupAsync(Context.ConnectionId, playerId.ToString());
        }
        
        await Clients.Caller.JoinedGame(gameState?.GetClientGameState(playerId));
    }

    public async Task PlayerReady(Guid playerId)
    {
        var gameState = GetGameState(playerId);
        gameState.SetPlayerReady(playerId);

        await Clients.Group(playerId.ToString()).SetReady(true);

        if (gameState.InProgress)
        {
            await Clients.Group(gameState.Id.ToString()).GameStarted();

            var randomPlayer = gameState.Players.Keys.ElementAt(Random.Shared.Next(0, gameState.Players.Count));
            await Clients.Groups(randomPlayer.ToString()).MoveTransition(true);
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

            var oponent = gameState?.Players.FirstOrDefault(p => p.Key != playerId) ??
                          throw new Exception("Couldn't find players game state");
            var oponentState = oponent.Value;
            var shotResult = oponentState.CheckShotResult(x, y);

            if (shotResult != null)
            {
                playerState.Shots.Push((x, y));

                await Clients.Group(oponent.Key.ToString()).UpdateCellState(shotResult, true);
                await Clients.Group(playerId.ToString()).UpdateEnemyCellState(shotResult);

                if (oponentState.Fleet.Ships.Count == 0)
                {
                    gameState.Stage = GameStageEnum.GameOver;
                    await Clients.Groups(playerId.ToString()).GameOver(true);
                    await Clients.Groups(oponent.Key.ToString()).GameOver(false);

                    return;
                }
                else if (!shotResult.Any(s => s.Value == CellState.hit))
                {
                    await Clients.Groups(oponent.Key.ToString()).MoveTransition(true);
                    await Clients.Groups(playerId.ToString()).MoveTransition(false);
                }
            }
        }
        else
        {
            var playerState = gameState.Players[playerId];
            var pos = x * gameState.Size + y;

            if (playerState.TryToUpdateState(x, y))
            {
                var cellState = playerState.Field[pos];
                await Clients.Group(playerId.ToString())
                    .UpdateCellState(new() { { pos, cellState } }, playerState.Fleet.Complete);
            }
        }
    }

    public async Task ClearField(Guid playerId)
    {
        var gameState = GetGameState(playerId);
        var playerState = gameState.Players[playerId];

        playerState.ClearField();

        await Clients.Group(playerId.ToString()).ClearField();
    }
}