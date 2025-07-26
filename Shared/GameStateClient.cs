using SeaBattle.Shared.Player;

namespace SeaBattle.Shared;

public class GameStateClient
{
    public PlayerInfo Player { get; set; } = null!;
    public string? OpponentsName { get; set; }
    public int FieldSize { get; set; }
    public GameStageEnum Stage { get; set; } = GameStageEnum.Setup;
    public bool FleetComplete { get; set; }

    public CellState[] OwnField { get; set; }
    public CellState[] EnemyField { get; set; }

    public void InitializeFields()
    {
        OwnField = new CellState[FieldSize * FieldSize];
        EnemyField = new CellState[FieldSize * FieldSize];
    }

    public void UpdateOwnFieldFromPlayer()
    {
        if (Player?.FieldState != null)
        {
            // Clear the field first
            Array.Fill(OwnField, CellState.empty);
            
            // Apply player's field state
            foreach (var kvp in Player.FieldState)
            {
                if (kvp.Key >= 0 && kvp.Key < OwnField.Length)
                {
                    OwnField[kvp.Key] = kvp.Value;
                }
            }
        }
    }
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