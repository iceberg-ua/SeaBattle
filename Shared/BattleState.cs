namespace SeaBattle.Shared;

public enum CellState { empty = 0, hit, miss, ship }

public class BattleState
{
    private int _size = 10;
    private int[,] _battleField;

    public BattleState()
    {
        InitBattleField(_size);
    }

    public bool InProgress { get; set; } = false;

    public void InitBattleField(int size)
    {
        _battleField = new int[size, size];
    }
}
