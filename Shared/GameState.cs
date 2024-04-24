using SeaBattle.Shared.Player;

namespace SeaBattle.Shared;

public enum CellState { empty = 0, ship, hit, miss }

public class GameState
{
    public GameState()
    {
        ID = Guid.NewGuid();

        Console.WriteLine($"Game created: {ID}");
    }

    public Guid ID { get; }

    public int Size { get; } = 10;

    public bool InProgress { get; private set; } = false;

    public Dictionary<Guid, PlayerState> Players { get; set; } = new(2);

    public PlayerState AddPlayer(string playerName)
    {
        if (Players.Count >= 2)
            throw new Exception("There can be only two players in the game");

        var playerState = new PlayerState(playerName, ID);

        Players.Add(playerState.PlayerId, playerState);

        return playerState;
    }

    public void SetPlayerReady(Guid playerId)
    {
        Players[playerId].Ready = true;

        if (Players.Count == 2 && Players.All(p => p.Value.Ready))
            InProgress = true;
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