using SeaBattle.Shared.Player;

namespace SeaBattle.Shared.Hub;

/// <summary>
/// Defines the contract for game-related communication between client and server.
/// </summary>
public interface IGameHub
{
    #region Server API (executed on server)

    /// <summary>
    /// Allows a player to join the game session.
    /// </summary>
    /// <param name="playerId">Unique identifier for the player</param>
    /// <param name="userName">Display name of the player</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task JoinGame(Guid playerId, string userName);

    /// <summary>
    /// Handles a player's click on a specific cell in the game grid.
    /// </summary>
    /// <param name="playerId">Unique identifier for the player making the move</param>
    /// <param name="x">X-coordinate of the clicked cell</param>
    /// <param name="y">Y-coordinate of the clicked cell</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task CellClicked(Guid playerId, int x, int y);

    /// <summary>
    /// Clears the player's game field, removing all ships and hit markers.
    /// </summary>
    /// <param name="playerId">Unique identifier for the player</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task ClearField(Guid playerId);

    /// <summary>
    /// Signals that a player has completed their ship placement and is ready to start.
    /// </summary>
    /// <param name="playerId">Unique identifier for the player</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task PlayerReady(Guid playerId);

    #endregion

    #region Server response (handled on client)

    /// <summary>
    /// Notifies the client that they have successfully joined the game.
    /// </summary>
    /// <param name="gameState">Current state of the game and player's state</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task JoinedGame(GameStateClient? gameState);

    /// <summary>
    /// Updates the complete game state on the client.
    /// Replaces multiple incremental update methods with a single comprehensive state update.
    /// </summary>
    /// <param name="gameState">Complete updated game state</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task UpdateGameState(GameStateClient gameState);

    /// <summary>
    /// Notifies the client that the game has officially started.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    Task GameStarted();

    /// <summary>
    /// Notifies the client that the game has ended.
    /// </summary>
    /// <param name="win">True if this player won, false if they lost</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task GameOver(bool win);

    #endregion
}