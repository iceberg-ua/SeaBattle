using Microsoft.AspNetCore.SignalR;
using SeaBattle.Shared;
using SeaBattle.Shared.Hub;

namespace SeaBattle.Server.Hubs;

class BattleHub(GlobalGameStorage storage) : Hub<IGameHub>
{
    private GlobalGameStorage GameStorage { get; } = storage;

    public async Task JoinGame(Guid playerId, string playerName)
    {
        GameState? gameState = playerId == Guid.Empty ? GameStorage.CreateGame() :
                                                        GameStorage.GetGameByPlayerId(playerId);

        if (gameState == null)
        {
            Console.WriteLine("Not game found");
            return;
        }

        if (!gameState.Players.TryGetValue(playerId, out var playerState) && !string.IsNullOrEmpty(playerName))
            playerState = gameState.AddPlayer(playerName);

        if (playerState != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, playerState.TableId.ToString());
            await Groups.AddToGroupAsync(Context.ConnectionId, playerState.PlayerId.ToString());

            var info = playerState.GetPlayerInfo();

            await Clients.Caller.JoinedGame(info);
        }
    }

    public async Task PlayerReady(Guid playerId)
    {
        var gameState = GameStorage.GetGameByPlayerId(playerId);

        gameState.Players[playerId].Ready = true;

        if (gameState.Players.Count == 2 && gameState.Players.All(p => p.Value.Ready))
        {
            await Clients.Group(gameState.ID.ToString()).GameStarted();
        }
    }

    public async Task CellClicked(Guid playerId, int x, int y)
    {
        var gameState = GameStorage.GetGameByPlayerId(playerId);

        if(gameState is null)
            throw new ArgumentNullException(nameof(gameState), "Game state wasn't found");

        if (gameState.InProgress)
        {
            var enemyState = (gameState?.Players.FirstOrDefault(p => p.Key != playerId).Value) ?? throw new Exception("Couldn't find players game state");
            var shotResult = enemyState.CheckShotResult(x, y);

            enemyState.Shots.Push((x, y));

            await Clients.Group(enemyState.PlayerId.ToString()).UpdateCellState(shotResult);
            await Clients.Group(playerId.ToString()).UpdateEnemyCellState(shotResult);
        }
        else
        {
            var playerState = gameState.Players[playerId];
            var pos = x * gameState.Size + y;

            if (playerState.TryToUpdateState(x, y))
            {
                var cellState = playerState.Field[pos];
                await Clients.Group(playerId.ToString()).UpdateCellState(new() { { pos, cellState } });
            }
        }

    }
}