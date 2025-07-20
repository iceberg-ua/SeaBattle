using SeaBattle.Shared;
using SeaBattle.Shared.Player;

namespace SeaBattle.Server.Services;

public class GameService
{
    public PlayerState AddPlayer(GameState game, string playerName)
    {
        if (game.Players.Count >= 2)
            throw new Exception("There can be only two players in the game");

        var playerState = new PlayerState(playerName, game.Id, game.Size);
        game.Players.Add(playerState.PlayerId, playerState);

        return playerState;
    }

    public void SetPlayerReady(GameState game, Guid playerId)
    {
        game.Players[playerId].SetReady();

        if (game.Players.Count == 2 && game.Players.All(p => p.Value.Ready))
            game.Stage = GameStageEnum.Game;
    }

    public PlayerState GetOpponent(GameState game, Guid playerId)
    {
        return game.Players.FirstOrDefault(s => s.Value.PlayerId != playerId).Value;
    }

    /// <summary>
    /// Retrieves the complete game state for a specific player, including all field states.
    /// </summary>
    /// <param name="game">The game state</param>
    /// <param name="playerId">The unique identifier of the player</param>
    /// <param name="includeEnemyField">Whether to include the enemy field state (for active games)</param>
    /// <returns>A complete GameStateClient object with all necessary state information</returns>
    public GameStateClient? GetClientGameState(GameState? game, Guid playerId, bool includeEnemyField = false)
    {
        if (game == null || !game.Players.ContainsKey(playerId))
            return null;

        var player = game.Players[playerId];
        var opponentInfo = GetOpponent(game, playerId);

        var gameState = new GameStateClient()
        {
            Player = player!.GetPlayerInfo(),
            OpponentsName = opponentInfo?.Name,
            FieldSize = game.Size,
            Stage = game.Stage
        };

        gameState.InitializeFields();
        gameState.UpdateOwnFieldFromPlayer();

        if (includeEnemyField && opponentInfo != null)
        {
            // Use the optimized pre-built enemy field state instead of reconstructing from shots
            Array.Copy(player.EnemyFieldState, gameState.EnemyField, player.EnemyFieldState.Length);
        }

        return gameState;
    }

    
    /// <summary>
    /// Creates a complete game state update for a player based on current game state.
    /// </summary>
    /// <param name="game">The current game state</param>
    /// <param name="playerId">The player to create the update for</param>
    /// <returns>Complete game state for the client</returns>
    public GameStateClient CreateGameStateUpdate(GameState game, Guid playerId)
    {
        return GetClientGameState(game, playerId, includeEnemyField: game.InProgress)!;
    }

}
