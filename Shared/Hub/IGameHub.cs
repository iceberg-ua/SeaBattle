using SeaBattle.Shared.Player;

namespace SeaBattle.Shared.Hub;
public interface IGameHub
{
    #region Server API (executed on server)

    Task JoinGame(Guid playerId, string userName);

    Task PlayerReady(Guid playerId);

    Task CellClicked(Guid playerId, int x, int y);

    Task ClearField(Guid playerId);

    #endregion

    #region Server reponse (handled on client)

    Task JoinedGame(PlayerInfo player);

    Task UpdateCellState(Dictionary<int, CellState> hits);

    Task UpdateEnemyCellState(Dictionary<int, CellState> hits);

    Task StateChanged(bool fleetReady);

    Task GameStarted();

    #endregion
}
