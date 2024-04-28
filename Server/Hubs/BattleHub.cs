using Microsoft.AspNetCore.SignalR;
using SeaBattle.Shared;
using SeaBattle.Shared.Hub;
using SeaBattle.Shared.Player;

namespace SeaBattle.Server.Hubs;

class BattleHub(GlobalGameStorage storage) : Hub<IGameHub>
{
    private GlobalGameStorage GameStorage { get; } = storage;

    private GameState GetGameState(Guid playerId) => GameStorage.GetGameByPlayerId(playerId) ?? throw new NullReferenceException("Game state wasn't found");

    public async Task JoinGame(Guid playerId, string playerName)
    {
        GameState? gameState = GameStorage.GetGameByPlayerId(playerId);   
        PlayerInfo? playerInfo = null;
        
        if(gameState is null)
        {
            if(!string.IsNullOrEmpty(playerName))
            {
                gameState = GameStorage.CreateGame();
                playerInfo = gameState.AddPlayer(playerName).GetPlayerInfo();
            }
        }
        else
        {
            gameState.Players.TryGetValue(playerId, out var playerState);
            playerInfo = playerState?.GetPlayerInfo();
        }

        if(playerInfo is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, gameState!.ID.ToString());
            await Groups.AddToGroupAsync(Context.ConnectionId, playerInfo.Id.ToString());
        }

        await Clients.Caller.JoinedGame(playerInfo);
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

            var oponent = gameState?.Players.FirstOrDefault(p => p.Key != playerId) ?? throw new Exception("Couldn't find players game state");
            var oponentState = oponent.Value;

            var shotResult = oponentState.CheckShotResult(x, y);

            if (shotResult != null)
            {
                playerState.Shots.Push((x, y));

                await Clients.Group(oponent.Key.ToString()).UpdateCellState(shotResult, true);
                await Clients.Group(playerId.ToString()).UpdateEnemyCellState(shotResult);

                gameState.PlayerInTurn = oponentState;

                if (oponentState.Fleet.Ships.Count == 0)
                {
                    gameState.PlayerInTurn = null;
                    await Clients.Groups(playerId.ToString()).GameOver(true);
                    await Clients.Groups(oponent.Key.ToString()).GameOver(false);

                    return;
                }
                else if(!shotResult.Any(s => s.Value == CellState.hit))
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