using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Linq;

namespace SeaBattle.Server.Hubs;

class BattleHub : Hub
{
    public async Task SendMessage(int x, int y)
    {
        await Clients.All.SendAsync("AtackCell", x, y);
    }

    private static Dictionary<Guid, int> _groups = new();

    public async Task JoinGroup(string userName)
    {
        var groupUid = _groups.FirstOrDefault(pair => pair.Value < 2).Key;
        var newTable = groupUid == Guid.Empty;

        if (newTable)
            groupUid = Guid.NewGuid();

        _groups.TryAdd(groupUid, 0);
        _groups[groupUid]++;

        var groupName = groupUid.ToString();

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("JoinedTable", userName,groupName, newTable);
    }
}