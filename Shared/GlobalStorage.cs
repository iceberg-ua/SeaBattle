namespace SeaBattle.Shared;

public class GlobalStorage
{
    private readonly Dictionary<Guid, GameState> _gamesStorage = new();

    public GameState GetGame(Guid gameId)
    {
        if(!_gamesStorage.TryGetValue(gameId, out var state))
            state = new GameState();

        return state;
    }
}
