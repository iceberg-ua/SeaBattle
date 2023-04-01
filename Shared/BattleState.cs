namespace SeaBattle.Shared;

public enum CellState { empty = 0, hit, miss, ship }

public class BattleState
{
    private int _size = 10;
    private int[,] _ownFleet;

    public BattleState()
    {
        InitBattleField(_size);
    }

    public bool InProgress { get; set; } = false;

    public int Size
    {
        get { return _size; }
        set
        {
            _size = value;
            InitBattleField(_size);
        }
    }

    public int[,] OwnFleet
    {
        get => _ownFleet;
    }

    public void InitBattleField(int size) => _ownFleet = new int[size, size];
}
