namespace SeaBattle.Shared;

public class PlayerState
{
    private readonly int _fieldSize;

    public PlayerState(string name, Guid tableId, int fieldSize = 10)
    {
        _fieldSize = fieldSize;

        Name = name;
        TableId = tableId;

        Armada = new CellState[_fieldSize * _fieldSize];
        Shots = new(_fieldSize * _fieldSize);
    }

    #region Properties

    public Guid TableId { get; }

    public string Name { get; } = default!;

    public int FieldSize => _fieldSize;

    public bool InProgress { get; set; } = false;

    public CellState[] Armada { get; }

    public Stack<(int, int)> Shots { get; }

    #endregion

    private CellState GetCell(int x, int y) => Armada[x * 10 + y];

    private void SetCell(int x, int y, CellState value) => Armada[x * 10 + y] = value;

    public bool TryToUpdateState(int x, int y)
    {
        if (!InProgress)
        {
            if (GetCell(x, y) == CellState.ship)
            {
                var ship = _ships.Where(s => s.Contains((x, y))).FirstOrDefault();
                ship?.Remove((x, y));
                SetCell(x, y, CellState.empty);
            }
            else if (CheckFormation(x, y))
                SetCell(x, y, CellState.ship);
            else
                return false;

        }

        return true;
    }

    private List<List<(int x, int y)>> _ships = new(10);
    private bool _full = false;

    private bool CheckFormation(int x, int y)
    {
        if (OnDiagonal(x, y))
            return false;

        SetCell(x, y, CellState.ship);

        return true;
    }

    private Dictionary<int, int> _shipCounts = new() { { 1, 4 }, { 2, 3 }, { 3, 2 }, { 4, 1 } };
    private readonly int _maxShipSize = 4;

    private bool CheckShipCount()
    {
        var shipGroups = _ships.Select(s => s.Count).GroupBy(s => s).OrderBy(g => g.Key);
        bool full = false;
       
        foreach (var group in shipGroups)
        {
            if (_shipCounts[group.Key] == group.Count())
                full = true;
            else if (_shipCounts[group.Key] < group.Count())
                full = false;
            else
                return false;
        }

        _full = full;
        return true;
    }

    private bool OnDiagonal(int x, int y) => CellIsOccupied(x - 1, y - 1) || CellIsOccupied(x + 1, y - 1) ||
                                             CellIsOccupied(x - 1, y + 1) || CellIsOccupied(x + 1, y + 1);

    private bool CellIsOccupied(int x, int y) =>  x >= 0 && x < FieldSize &&
                                                  y >= 0 && y < FieldSize && GetCell(x, y) == CellState.ship;
}
