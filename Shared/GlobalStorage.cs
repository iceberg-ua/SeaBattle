namespace SeaBattle.Shared;

public class GlobalStorage
{
    private readonly Dictionary<Guid, GameState> _gamesStorage = new();
    private GameState? _freePlaceGame = null;

    public GameState GetGame()
    {
        if(_freePlaceGame is null)
        {
            var game = new GameState();
            _gamesStorage.Add(game.ID,game);
            _freePlaceGame = game;
            return game;
        }
        else
        {
            var game = _freePlaceGame;
            _freePlaceGame.IsNew = false;
            _freePlaceGame = null;
            return game;
        }

    }
}
