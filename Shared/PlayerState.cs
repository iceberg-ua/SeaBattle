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
                SetCell(x, y, CellState.empty);
            else if (CheckFormation(x, y))
                SetCell(x, y, CellState.ship);
            else
                return false;

        }

        return true;
    }

    private bool CheckFormation(int x, int y)
    {
        if (OnDiagonal(x, y))
            return false;

        SetCell(x, y, CellState.ship);

        for (int i = 0; i < FieldSize; i++)
        {
            for (int j = 0; j < FieldSize; j++)
            {
                //if( GetCell(x, y) == CellState.Deck)
                //    CheckShip(i, j);
            }
        }

        return true;
    }

    private bool OnDiagonal(int x, int y) => CellIsOccupied(x - 1, y - 1) || CellIsOccupied(x + 1, y - 1) ||
                                             CellIsOccupied(x - 1, y + 1) || CellIsOccupied(x + 1, y + 1);

    private bool CellIsOccupied(int x, int y) =>  x >= 0 && x < FieldSize &&
                                                  y >= 0 && y < FieldSize && GetCell(x, y) == CellState.ship;
}
