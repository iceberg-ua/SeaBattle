using Microsoft.AspNetCore.SignalR;
using SeaBattle.Shared;
using SeaBattle.Shared.Hub;

namespace SeaBattle.Server.Hubs;

class BattleHub(GlobalGameStorage storage) : Hub<IGameHub>
{
    private GlobalGameStorage GameStorage { get; } = storage;

    private GameState GetGameState(Guid playerId) => GameStorage.GetGameByPlayerId(playerId) ?? throw new NullReferenceException("Game state wasn't found");

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
        var gameState = GetGameState(playerId);

        gameState.SetPlayerReady(playerId);

        await Clients.Group(playerId.ToString()).SetReady(true);

        if(gameState.InProgress)
        {
            await Clients.Group(gameState.ID.ToString()).GameStarted();

            var randomPlayer = gameState.Players.Keys.ElementAt(Random.Shared.Next(0, gameState.Players.Count));
            await Clients.Groups(randomPlayer.ToString()).MoveTransition();
        }
    }

    public async Task CellClicked(Guid playerId, int x, int y)
    {
        var gameState = GetGameState(playerId);

        if (gameState.InProgress)
        {
            //var enemyState = (gameState?.Players.FirstOrDefault(p => p.Key != playerId).Value) ?? throw new Exception("Couldn't find players game state");
            //var shotResult = enemyState.CheckShotResult(x, y);

            //enemyState.Shots.Push((x, y));

            //await Clients.Group(enemyState.PlayerId.ToString()).UpdateCellState(shotResult);
            //await Clients.Group(playerId.ToString()).UpdateEnemyCellState(shotResult);
            gameState.Players[playerId].InTurn = false;
            var oponent = gameState.Players.Where(x => x.Key != playerId).FirstOrDefault();
            oponent.Value.InTurn = true;
            await Clients.Groups(oponent.Key.ToString()).MoveTransition();
        }
        else
        {
            var playerState = gameState.Players[playerId];
            var pos = x * gameState.Size + y;

            if (playerState.TryToUpdateState(x, y))
            {
                var cellState = playerState.Field[pos];
                await Clients.Group(playerId.ToString()).UpdateCellState(new() { { pos, cellState } }, playerState.Fleet.Complete);
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