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

    private readonly int _maxShipSize = 4;
    private readonly Dictionary<int, int> _shipCounts = new() { { 1, 4 }, { 2, 3 }, { 3, 2 }, { 4, 1 } };
    private readonly List<List<(int x, int y)>> _ships = new(10);
    private bool _full = false;

    private CellState GetCell(int x, int y) => Armada[x * 10 + y];

    private void SetCell(int x, int y, CellState value) => Armada[x * 10 + y] = value;

    
    /// <summary>
    /// Update the ships on the battlefiled
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>true - if legal, false - if not</returns>
    private bool UpdateFormation(int x, int y)
    {
        if (_full || OnDiagonal(x, y))
            return false;

        var existingShips = _ships.Where(ship => ship.Contains((x - 1, y)) ||
                                                 ship.Contains((x + 1, y)) ||
                                                 ship.Contains((x, y - 1)) ||
                                                 ship.Contains((x, y + 1)));

        if (existingShips.Count() > 1)
        {
            if (existingShips.Sum(s => s.Count) + 1 <= 4)
            {
                var newShip = existingShips.SelectMany(s => s).Concat([(x, y)]).OrderBy(s => s.x).OrderBy(s => s.y).ToList();

                _ships.RemoveAll(s => existingShips.Contains(s));
                _ships.Add(newShip);
            }
            else
                return false;
        }
        else if (existingShips.Count() == 1)
        {
            var ship = existingShips.First();

            if (ship.Count == 4)
                return false;
            
            ship.Add((x, y));
        }
        else
            _ships.Add([ (x, y) ]);

        //_full = CheckShipsCount();

        return true;
    }

    private bool CheckShipsCount()
    {
        var shipGroups = _ships.Select(s => s.Count).GroupBy(s => s).OrderBy(g => g.Key);
        bool full = true;
       
        foreach (var group in shipGroups)
        {
            if (_shipCounts[group.Key] == group.Count())
                full &= true;
            else if (_shipCounts[group.Key] < group.Count())
                full &= false;
            else
                throw new ArgumentOutOfRangeException("There is more ships then allowed");
        }

        return full;
    }

    private bool OnDiagonal(int x, int y) => CellIsOccupied(x - 1, y - 1) || CellIsOccupied(x + 1, y - 1) ||
                                             CellIsOccupied(x - 1, y + 1) || CellIsOccupied(x + 1, y + 1);

    private bool CellIsOccupied(int x, int y) =>  x >= 0 && x < FieldSize &&
                                                  y >= 0 && y < FieldSize && GetCell(x, y) == CellState.ship;

    public bool TryToUpdateState(int x, int y)
    {
        if (!InProgress)
        {
            if (CellIsOccupied(x, y))
            {
                var ship = _ships.Where(s => s.Contains((x, y))).FirstOrDefault();
                ship?.Remove((x, y));
                SetCell(x, y, CellState.empty);
            }
            else if (UpdateFormation(x, y))
                SetCell(x, y, CellState.ship);
            else
                return false;

        }

        return false;
    }
}
