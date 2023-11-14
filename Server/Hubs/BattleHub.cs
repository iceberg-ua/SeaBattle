using Microsoft.AspNetCore.SignalR;
using SeaBattle.Shared;
using SeaBattle.Shared.Hub;

namespace SeaBattle.Server.Hubs;

class BattleHub : Hub<IGameHub>
{
    public BattleHub(GlobalStorage storage)
    {
        Storage = storage;
    }

    private GlobalStorage Storage { get; } = default!;

    public async Task JoinGame(Guid gameId, string playerName)
    {
        PlayerState playerState = Storage.GetGame(gameId).AddPlayer(playerName);

        await Groups.AddToGroupAsync(Context.ConnectionId, gameId.ToString());
        await Clients.Caller.JoinedGame(playerState);
    }
}