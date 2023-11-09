namespace SeaBattle.Shared;

public class GameState
{
    public GameState()
    {
        ID = Guid.NewGuid();
    }

    public Guid ID { get; }

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

    public bool InProgress { get; set; } = false;

    public int Size { get; set; } = 10;

    public List<string> Opponents { get; } = new(2);

    public void AddPlayer(string username)
    {
        if (Opponents.Count >= 2)
            throw new Exception("There can be only two players in the game");

        Opponents.Add(username);
    }
}
