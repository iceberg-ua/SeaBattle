namespace SeaBattle.Shared.Hub;
public interface IGameHub
{
    #region Client API
    
    Task JoinGame(Guid gameId, Guid playerId, string userName);

    Task PlayerReady(Guid gameId, Guid playerId);

    Task CellClicked(Guid gameId, Guid playerId, int x, int y);

    #endregion

    #region Server reponse

    Task JoinedGame(PlayerState state, Guid playerId);

    Task UpdateCellState(Dictionary<int, CellState> hits, bool own);

    Task StateChanged(bool fleetReady);

    Task GameStarted(Guid gameId);

    #endregion
}
