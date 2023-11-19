namespace SeaBattle.Shared;

public enum CellState { empty = 0, ship, hit, miss }

public class BattleState
{
    public BattleState(GameState gameState)
    {
        BattleField = new int[gameState.Size, gameState.Size];
    }

    public int[,] BattleField { get; set; }
}
