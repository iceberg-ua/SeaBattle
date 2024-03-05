using SeaBattle.Shared.Player;

namespace SeaBattle.Shared;

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
