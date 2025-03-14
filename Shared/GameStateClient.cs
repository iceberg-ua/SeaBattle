using SeaBattle.Shared.Player;

namespace SeaBattle.Shared;

public class GameStateClient
{
    public PlayerInfo? Player { get; set; }
    public string? OpponentsName { get; set; }
    public GameStageEnum Stage { get; set; } = GameStageEnum.Setup;
}

public enum GameStageEnum
{
    Setup,
    Waiting,
    Game,
    GameOver,
}

public enum PlayerStateEnum
{
    Formation,
    Ready,
    InTurn,
    WaitingForTurn
}