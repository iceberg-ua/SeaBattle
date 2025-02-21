namespace SeaBattle.Shared;

public class GlobalGameStorage
{
    private readonly Dictionary<Guid, GameState> _gamesStorage = [];
    private GameState? _vacantGame = null;

    public GameState? GetGameById(Guid playerId)
    {
        _gamesStorage.TryGetValue(playerId, out var game);

        return game;
    }

    public GameState? GetGameByPlayerId(Guid playerId)
    {
        return _gamesStorage.Values.FirstOrDefault(g => g.Players.Keys.Contains(playerId));
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
            _gamesStorage.Add(game.Id, game);
            _vacantGame = game;
        }

        return game;
    }
}
