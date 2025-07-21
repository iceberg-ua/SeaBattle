namespace SeaBattle.Shared.Domain;

/// <summary>
/// Domain abstraction for game storage and retrieval.
/// Separates domain logic from infrastructure concerns like thread-safety and persistence.
/// </summary>
public interface IGameRepository
{
    /// <summary>
    /// Finds a game by player ID.
    /// </summary>
    /// <param name="playerId">The player's unique identifier</param>
    /// <returns>The game state if found, otherwise null</returns>
    GameState? GetGameByPlayerId(Guid playerId);

    /// <summary>
    /// Creates a new game or joins an existing vacant game.
    /// </summary>
    /// <returns>The created or joined game state</returns>
    GameState CreateOrJoinGame();

    /// <summary>
    /// Removes a game from storage.
    /// </summary>
    /// <param name="gameId">The game's unique identifier</param>
    void RemoveGame(Guid gameId);

    /// <summary>
    /// Saves or updates a game state.
    /// </summary>
    /// <param name="gameState">The game state to save</param>
    void SaveGame(GameState gameState);
}