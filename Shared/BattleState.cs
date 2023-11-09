namespace SeaBattle.Shared;

public enum CellState { empty = 0, hit, miss, ship }

public class BattleState
{
    public BattleState(GameState gameState)
    {
        BattleField = new int[gameState.Size, gameState.Size];
    }

    public int[,] BattleField { get; set; }
}
