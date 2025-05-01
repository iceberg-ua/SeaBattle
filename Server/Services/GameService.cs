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
    /// Retrieves the game state for a specific player, including player information,
    /// opponent's name, and the current stage of the game.
    /// </summary>
    /// <param name="playerId">The unique identifier of the player.</param>
    /// <returns>A GameStateClient object containing the player's game state information. The object for client side state handling.</returns>
    public GameStateClient? GetClientGameState(GameState? game, Guid playerId)
    {
        if (game == null)
            return null;

        var playerInfo = game.Players[playerId]?.GetPlayerInfo();
        var opponentInfo = GetOpponent(game, playerId);

        return new GameStateClient()
        {
            Player = playerInfo!,
            OpponentsName = opponentInfo?.Name,
            FieldSize = game.Size,
            Stage = game.Stage
        };
    }
}
