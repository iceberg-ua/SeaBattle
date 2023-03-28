using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace SeaBattle.Server.Hubs;

class BattleHub : Hub
{
    public async Task SendMessage(int x, int y)
    {
        await Clients.All.SendAsync("AtackCell", x, y);
    }
}