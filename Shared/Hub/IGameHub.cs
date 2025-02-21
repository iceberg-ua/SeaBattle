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
    /// Updates the state of cells on the player's board after an action.
    /// </summary>
    /// <param name="hits">Dictionary mapping cell indices to their new states</param>
    /// <param name="full">Indicates if this is a full board update</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task UpdateCellState(Dictionary<int, CellState> hits, bool full);

    /// <summary>
    /// Clears all cells on the client's game field.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    Task ClearField();

    /// <summary>
    /// Updates the ready status of the player.
    /// </summary>
    /// <param name="ready">True if player is ready, false otherwise</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SetReady(bool ready);

    /// <summary>
    /// Updates the state of cells on the enemy's board after an action.
    /// </summary>
    /// <param name="hits">Dictionary mapping cell indices to their new states</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task UpdateEnemyCellState(Dictionary<int, CellState> hits);

    /// <summary>
    /// Notifies the client that the fleet placement state has changed.
    /// </summary>
    /// <param name="fleetReady">True if fleet placement is complete, false otherwise</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task StateChanged(bool fleetReady);

    /// <summary>
    /// Notifies the client that the game has officially started.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    Task GameStarted();

    /// <summary>
    /// Signals a change in turn between players.
    /// </summary>
    /// <param name="move">True if it's this player's turn, false otherwise</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task MoveTransition(bool move);

    /// <summary>
    /// Notifies the client that the game has ended.
    /// </summary>
    /// <param name="win">True if this player won, false if they lost</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task GameOver(bool win);

    #endregion
}
