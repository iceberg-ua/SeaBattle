using SeaBattle.Shared.Player;

namespace SeaBattle.Shared;

public class GameStateClient
{
    public PlayerInfo? Player { get; set; }
    public string? OpponentsName { get; set; }
    public int FieldSize { get; set; }
    public GameStageEnum Stage { get; set; } = GameStageEnum.Setup;
}

public enum GameStageEnum
{
    Setup,
    Game,
    GameOver,
}

public enum PlayerStateEnum
{
    Formation,
    Ready,
    InTurn,
    WaitingForTurn,
    Won,
    Lost
}