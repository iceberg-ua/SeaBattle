using SeaBattle.Shared.Player;

namespace SeaBattle.Shared;

public class GameStateClient
{
    public PlayerInfo? Player { get; set; } = null;
    public GameStageEnum Stage { get; set; } = GameStageEnum.SigningIn;

    public void SwitchToSetupStage()
    {
        Stage = GameStageEnum.Setup;
    }
}

public enum GameStageEnum
{
    SigningIn,
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