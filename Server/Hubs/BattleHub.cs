using Microsoft.AspNetCore.SignalR;
using SeaBattle.Shared;

namespace SeaBattle.Server.Hubs;

class BattleHub : Hub
{
    public BattleHub(GlobalStorage storage)
    {
        Storage = storage;
    }

    public GlobalStorage? Storage { get; }

    public async Task SendMessage(int x, int y)
    {
        await Clients.All.SendAsync("AtackCell", x, y);
    }

    private static Dictionary<Guid, int> _groups = new();

    public async Task JoinGroup(string userName)
    {
        var game = (Storage?.GetGame()) ?? throw new Exception("No possible to create a game");

        game.AddPlayer(userName);
        var groupName = game.ID.ToString();

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("JoinedTable", userName, groupName, game.IsNew);
    }
}