using Microsoft.AspNetCore.SignalR;
using SeaBattle.Shared;
using SeaBattle.Shared.Hub;

namespace SeaBattle.Server.Hubs;

class BattleHub : Hub<IGameHub>
{
    public BattleHub(GlobalGameStorage storage)
    {
        GameStorage = storage;
    }

    private GlobalGameStorage GameStorage { get; } = default!;

    public async Task JoinGame(Guid gameId, Guid playerId, string playerName)
    {
        Console.WriteLine($"REQUESTED: table - {gameId}, playerId-{playerId}, name - {playerName}");

        GameState? gameState = gameId == Guid.Empty ? GameStorage.CreateGame() : 
                                                      GameStorage.GetGame(gameId);

        if (gameState == null)
        {
            Console.WriteLine("Not game found");
            return;
        }

        if (!gameState.Players.TryGetValue(playerId, out var playerState) && !string.IsNullOrEmpty(playerName))
            playerState = gameState.AddPlayer(playerName);

        if (playerState != null)
        {
            Console.WriteLine($"Found player state: game - {playerState.TableId}, player - {playerState.PlayerId}");
            await Groups.AddToGroupAsync(Context.ConnectionId, playerState.TableId.ToString());
            await Groups.AddToGroupAsync(Context.ConnectionId, playerState.PlayerId.ToString());
            Console.WriteLine($"Add connection to Group: {gameId}");
            await Clients.Caller.JoinedGame(playerState, playerState.PlayerId);
        }
    }

    public async Task PlayerReady(Guid gameId, Guid playerId)
    {
        var gameState = GameStorage.GetGame(gameId);

        gameState.Players[playerId].Ready = true;

        if(gameState.Players.Count == 2 && gameState.Players.All(p => p.Value.Ready))
        {
            Console.WriteLine($"Broadcasting to Group: {gameId}");
            await Clients.Group(gameId.ToString()).GameStarted(gameId);
        }
    }

    public async Task CellClicked(Guid gameId, Guid playerId, int x, int y)
    {
        var gameState = GameStorage.GetGame(gameId);
        var enemyState = gameState?.Players.FirstOrDefault(p => p.Key != playerId).Value;

        if (enemyState is null)
            throw new Exception("Couldn't find players game state");

        var shotResult = enemyState.CheckShotResult(x, y);
        enemyState.Shots.Push((x, y));

        await Clients.Group(enemyState.PlayerId.ToString()).UpdateCellState(shotResult, true);
        await Clients.Group(playerId.ToString()).UpdateCellState(shotResult, false);

    }
}