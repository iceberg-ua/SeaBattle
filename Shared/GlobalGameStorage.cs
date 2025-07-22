using System.Collections.Concurrent;

namespace SeaBattle.Shared;

public class GlobalGameStorage
{
    private readonly ConcurrentDictionary<Guid, GameState> _gamesStorage = new();
    private GameState? _vacantGame;
    private readonly Lock _vacantGameLock = new();
    
    public GameState? GetGameByPlayerId(Guid playerId)
    {
        return _gamesStorage.Values.FirstOrDefault(g => g.Players.ContainsKey(playerId));
    }

    public GameState? GetGameById(Guid gameId)
    {
        return _gamesStorage.TryGetValue(gameId, out var game) ? game : null;
    }

    public GameState CreateGame()
    {
        lock (_vacantGameLock)
        {
            if (_vacantGame != null)
            {
                var game = _vacantGame;
                _vacantGame = null;
                return game;
            }
            else
            {
                var game = new GameState();
                _gamesStorage.TryAdd(game.Id, game);
                _vacantGame = game;
                return game;
            }
        }
    }

    public void RemoveGame(Guid gameId)
    {
        _gamesStorage.TryRemove(gameId, out _);
        
        lock (_vacantGameLock)
        {
            if (_vacantGame?.Id == gameId)
            {
                _vacantGame = null;
            }
        }
    }
}
