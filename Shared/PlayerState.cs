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

    public bool TryToUpdateState(int x, int y)
    {
        if (!InProgress)
        {
            var value = Armada[x * 10 + y] == CellState.ship ? CellState.empty : CellState.ship;
            Armada[x * 10 + y] = value;
        }

        return true;
    }
}
