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

    // State versioning and validation properties
    public long StateVersion { get; set; } = 0;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public string? StateChecksum { get; set; }

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

    /// <summary>
    /// Calculates a simple checksum for state validation
    /// </summary>
    public string CalculateChecksum()
    {
        var data = $"{Player?.Id}_{Stage}_{FieldSize}_{FleetComplete}";
        
        // Add field data to checksum
        if (OwnField != null)
        {
            data += "_" + string.Join("", OwnField.Select(c => (int)c));
        }
        if (EnemyField != null)
        {
            data += "_" + string.Join("", EnemyField.Select(c => (int)c));
        }

        // Simple hash calculation
        return Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(data).Take(8).ToArray());
    }

    /// <summary>
    /// Updates the state metadata when the state changes
    /// </summary>
    public void UpdateStateMetadata()
    {
        LastUpdated = DateTime.UtcNow;
        StateChecksum = CalculateChecksum();
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