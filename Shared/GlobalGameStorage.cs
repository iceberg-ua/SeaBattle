namespace SeaBattle.Shared;

public class GlobalGameStorage
{
    private readonly Dictionary<Guid, GameState> _gamesStorage = new();
    private GameState? _vacantGame = null;

    public GameState? GetGame(Guid gameId)
    {
        _gamesStorage.TryGetValue(gameId, out var game);

        return game;
    }

    public GameState CreateGame()
    {
        GameState game;

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

        return game;
    }
}
