using System.Collections.Concurrent;
using SeaBattle.Shared;
using SeaBattle.Shared.Domain;

namespace SeaBattle.Server.Infrastructure.Repositories;

/// <summary>
/// Infrastructure implementation of IGameRepository using in-memory storage.
/// Handles all infrastructure concerns like thread-safety and concurrency.
/// </summary>
public class InMemoryGameRepository : IGameRepository
{
    private readonly ConcurrentDictionary<Guid, GameState> _gamesStorage = new();
    private GameState? _vacantGame;
    private readonly Lock _vacantGameLock = new();

    public GameState? GetGameByPlayerId(Guid playerId)
    {
        return _gamesStorage.Values.FirstOrDefault(g => g.Players.ContainsKey(playerId));
    }

    public GameState CreateOrJoinGame()
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

    public void SaveGame(GameState gameState)
    {
        _gamesStorage.AddOrUpdate(gameState.Id, gameState, (key, existing) => gameState);
    }
}