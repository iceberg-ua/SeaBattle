using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace SeaBattle.Server.Hubs;

class BattleHub : Hub
{
      public async Task AtackCell(int x, int y)
    {
        await Clients.All.SendAsync("HitCell", x, y);
    }
}