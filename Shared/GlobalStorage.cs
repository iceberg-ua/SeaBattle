namespace SeaBattle.Shared;

public class GlobalStorage
{
    private readonly Dictionary<Guid, GameState> _gamesStorage = new();
    private GameState? _vacantGame = null;

    public GameState GetGame(Guid gameId)
    {
        if (!_gamesStorage.TryGetValue(gameId, out var game))
        {
            if (_vacantGame != null)
            {
                game = _vacantGame;
                _vacantGame = null;
            }
            else
            {
                game = new GameState();
                _gamesStorage.Add(game.ID, game);
                _vacantGame = game;
            }
        }

        return game;
    }
}
