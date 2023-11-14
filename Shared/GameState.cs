namespace SeaBattle.Shared;

public class GameState
{
    public GameState()
    {
        ID = Guid.NewGuid();
    }

    public Guid ID { get; }

    public int Size { get; } = 10;

    public bool InProgress { get; private set; } = false;

    public bool Finished => Winner is not null;

    public Dictionary<string, PlayerState> Players { get; set; } = new(2);

    public string? Winner { get; private set; } = null;

    public PlayerState AddPlayer(string playerName)
    {
        if (Players.Count >= 2)
            throw new Exception("There can be only two players in the game");

        var playerState = new PlayerState(playerName, ID);

        Players.Add(playerName, playerState);

        return playerState;
    }
}
