namespace SeaBattle.Shared;

public class PlayerState
{
    public PlayerState(string name, Guid tableId)
    {
        Name = name;
        TableId = tableId;
    }
    public Guid TableId { get; }

    public string Name { get; } = default!;

    public bool InProgress { get; set; } = false;

    public int[] Armada { get; } = new int[100];

    public List<(int, int)> Shots { get; } = new(100);
}
