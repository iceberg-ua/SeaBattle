namespace SeaBattle.Shared.Hub;
public interface IGameHub
{
    #region Client API
    
    Task JoinGame(Guid gameId, Guid playerId, string userName);

    Task PlayerReady(Guid gameId, Guid playerId);

    #endregion

    #region Server reponse

    Task JoinedGame(PlayerState state);

    Task GameStarted();

    #endregion
}
