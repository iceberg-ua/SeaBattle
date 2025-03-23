using SeaBattle.Shared.Player;

namespace SeaBattle.Shared;

public enum CellState
{
    empty = 0,
    ship,
    hit,
    miss
}

public class GameState
{
    public Guid Id { get; } = Guid.NewGuid();

    public int Size { get; } = 10;

    public GameStageEnum Stage { get; set; } = GameStageEnum.Setup;

    public bool InProgress => Stage == GameStageEnum.Game;

    public Dictionary<Guid, PlayerState> Players { get; } = new(2);

    public PlayerState AddPlayer(string playerName)
    {
        if (Players.Count >= 2)
            throw new Exception("There can be only two players in the game");

        var playerState = new PlayerState(playerName, Id, Size);
        Players.Add(playerState.PlayerId, playerState);

        return playerState;
    }

    public PlayerState GetOpponent(Guid playerId)
    {
        return Players.FirstOrDefault(s => s.Value.PlayerId != playerId).Value;
    }

    public void SetPlayerReady(Guid playerId)
    {
        Players[playerId].SetReady();

        if (Players.Count == 2 && Players.All(p => p.Value.Ready))
            Stage = GameStageEnum.Game;
    }

    /// <summary>
    /// Retrieves the game state for a specific player, including player information,
    /// opponent's name, and the current stage of the game.
    /// </summary>
    /// <param name="playerId">The unique identifier of the player.</param>
    /// <returns>A GameStateClient object containing the player's game state information. The object for client side state handling.</returns>
    public GameStateClient GetClientGameState(Guid playerId)
    {
        var playerInfo = Players[playerId]?.GetPlayerInfo();
        var opponentInfo = GetOpponent(playerId);

        return new GameStateClient()
        {
            Player = playerInfo,
            OpponentsName = opponentInfo?.Name,
            Stage = Stage
        };
    }
}

///NOTES:
//
// * introduce mode property which will handle cell click (preparing mode, battle mode)
// * refresh after game was over shows wrong state
// * after each move show the text result in a lable


// show name of the opponent
// show the rest of the fleet to shot down/to place
// better indicate change 
// show the rest of the opponents fleet after game change