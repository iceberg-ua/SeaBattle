using SeaBattle.Shared.Player;

namespace SeaBattle.Shared.Hub;
public interface IGameHub
{
    #region Client API
    
    Task JoinGame(Guid playerId, string userName);

    Task PlayerReady(Guid playerId);

    Task CellClicked(Guid playerId, int x, int y);

    Task ClearField(Guid playerId);

    #endregion

    #region Server reponse

    Task JoinedGame(PlayerInfo player);

    Task UpdateCellState(Dictionary<int, CellState> hits, bool own);

    Task UpdateEnemyCellState(Dictionary<int, CellState> hits, bool own);

    Task StateChanged(bool fleetReady);

    Task GameStarted();

    #endregion
}
