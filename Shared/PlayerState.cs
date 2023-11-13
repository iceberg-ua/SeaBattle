namespace SeaBattle.Shared;

public class PlayerState
{
    public string Name { get; set; } = default!;

    public Guid TableId { get; set; }

    public bool IsLoggedIn { get; set; } = false;

    public bool InProgress { get; set; } = false;
}
