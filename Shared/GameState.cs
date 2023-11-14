namespace SeaBattle.Shared;

public class GameState
{
    public GameState()
    {
        ID = Guid.NewGuid();
    }

    public Guid ID { get; }

    public int Size { get; } = 10;

    private bool _isNew = true;

    public bool IsNew 
    { 
        get => _isNew;
        set
        {
            if (_isNew)
                _isNew = value;
        } 
    }

    public bool InProgress { get; private set; } = false;

    public bool Finished => Winner is not null;

    public Dictionary<string, PlayerState> Players { get; set; } = new(2);

    public string? Winner { get; private set; } = null;

    public void AddPlayer(string username)
    {
        if (Players.Count >= 2)
            throw new Exception("There can be only two players in the game");

        Players.Add(username, new PlayerState(username, ID));
    }
}
