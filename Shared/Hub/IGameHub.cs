namespace SeaBattle.Shared.Hub;
public interface IGameHub
{
    #region Client API
    
    Task JoinGame(Guid gameId, string userName);

    #endregion

    #region Server reponse

    Task JoinedGame(PlayerState state);

    #endregion
}
