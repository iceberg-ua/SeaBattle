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
            var oponent = (gameState?.Players.FirstOrDefault(p => p.Key != playerId)) ?? throw new Exception("Couldn't find players game state");
            var oponentState = oponent.Value;
            var playerState = gameState.Players[playerId];
            var shotResult = oponentState.CheckShotResult(x, y);

            if (shotResult != null)
            {
                playerState.Shots.Push((x, y));

                await Clients.Group(oponent.Key.ToString()).UpdateCellState(shotResult, true);
                await Clients.Group(playerId.ToString()).UpdateEnemyCellState(shotResult);

                playerState.InTurn = false;
                oponentState.InTurn = true;

                if (oponentState.Fleet.Ships.Count == 0)
                {
                    await Clients.Groups(playerId.ToString()).GameOver(true);
                    await Clients.Groups(oponent.Key.ToString()).GameOver(false);
                    return;
                }
                else
                    await Clients.Groups(oponent.Key.ToString()).MoveTransition();
            }
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