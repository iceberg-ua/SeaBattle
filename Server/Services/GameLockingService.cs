using System.Collections.Concurrent;

namespace SeaBattle.Server.Services;

/// <summary>
/// Service that provides per-game locking to prevent race conditions in concurrent operations.
/// Ensures atomic game state operations like shot processing, turn switching, and game state updates.
/// </summary>
public class GameLockingService
{
    private readonly ConcurrentDictionary<Guid, Lock> _gameLocks = new();

    /// <summary>
    /// Executes an action with exclusive access to the specified game.
    /// Prevents race conditions by ensuring only one operation per game can execute at a time.
    /// </summary>
    /// <param name="gameId">The ID of the game to lock</param>
    /// <param name="action">The action to execute with exclusive game access</param>
    public void ExecuteWithGameLock(Guid gameId, Action action)
    {
        var gameLock = _gameLocks.GetOrAdd(gameId, _ => new Lock());
        
        lock (gameLock)
        {
            action();
        }
    }

    /// <summary>
    /// Executes an async action with exclusive access to the specified game.
    /// Prevents race conditions by ensuring only one operation per game can execute at a time.
    /// </summary>
    /// <param name="gameId">The ID of the game to lock</param>
    /// <param name="action">The async action to execute with exclusive game access</param>
    public async Task ExecuteWithGameLockAsync(Guid gameId, Func<Task> action)
    {
        var gameLock = _gameLocks.GetOrAdd(gameId, _ => new Lock());
        
        lock (gameLock)
        {
            // Execute async action synchronously within the lock to maintain atomicity
            action().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Executes a function with exclusive access to the specified game and returns a result.
    /// </summary>
    /// <typeparam name="T">The type of result to return</typeparam>
    /// <param name="gameId">The ID of the game to lock</param>
    /// <param name="func">The function to execute with exclusive game access</param>
    /// <returns>The result of the function execution</returns>
    public T ExecuteWithGameLock<T>(Guid gameId, Func<T> func)
    {
        var gameLock = _gameLocks.GetOrAdd(gameId, _ => new Lock());
        
        lock (gameLock)
        {
            return func();
        }
    }

    /// <summary>
    /// Executes an async function with exclusive access to the specified game and returns a result.
    /// </summary>
    /// <typeparam name="T">The type of result to return</typeparam>
    /// <param name="gameId">The ID of the game to lock</param>
    /// <param name="func">The async function to execute with exclusive game access</param>
    /// <returns>The result of the function execution</returns>
    public async Task<T> ExecuteWithGameLockAsync<T>(Guid gameId, Func<Task<T>> func)
    {
        var gameLock = _gameLocks.GetOrAdd(gameId, _ => new Lock());
        
        lock (gameLock)
        {
            // Execute async function synchronously within the lock to maintain atomicity
            return func().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Removes the lock for a completed game to prevent memory leaks.
    /// Should be called when a game is finished and removed from storage.
    /// </summary>
    /// <param name="gameId">The ID of the game to clean up</param>
    public void CleanupGameLock(Guid gameId)
    {
        _gameLocks.TryRemove(gameId, out _);
    }
}