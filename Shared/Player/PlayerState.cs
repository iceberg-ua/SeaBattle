using SeaBattle.Shared.Ships;

namespace SeaBattle.Shared.Player;

public class PlayerState
{
    private readonly int _fieldSize;

    public PlayerState(string name, Guid tableId, int fieldSize = 10)
    {
        _fieldSize = fieldSize;

        Name = name;
        TableId = tableId;
        PlayerId = Guid.NewGuid();

        Field = new CellState[_fieldSize * _fieldSize];
        Shots = new(_fieldSize * _fieldSize);
    }

    #region Properties

    public Guid TableId { get; }

    public Guid PlayerId { get; set; }

    public string Name { get; } = default!;

    public int FieldSize => _fieldSize;

    public bool InProgress { get; set; } = false;

    public bool Ready { get; set; } = false;

    public CellState[] Field { get; private set; }

    public Fleet Fleet { get; private set; } = new();

    public Stack<(int, int)> Shots { get; }

    #endregion

    private CellState GetCell(int x, int y) => Field[x * 10 + y];

    private bool CellIsOccupied(int x, int y) => x >= 0 && x < FieldSize &&
                                                 y >= 0 && y < FieldSize && GetCell(x, y) == CellState.ship;

    private void SetCell(int x, int y, CellState value) => Field[x * 10 + y] = value;

    private bool OnDiagonal(int x, int y) => CellIsOccupied(x - 1, y - 1) || CellIsOccupied(x + 1, y - 1) ||
                                             CellIsOccupied(x - 1, y + 1) || CellIsOccupied(x + 1, y + 1);

    public PlayerInfo GetPlayerInfo()
    {
        var fieldState = new Dictionary<int, CellState>();

        foreach (var ship in Fleet.Ships)
        {
            foreach (ShipDeck deck in ship)
            {
                fieldState.Add(deck.X * _fieldSize + deck.Y, deck.State);
            }
        }

        return new(PlayerId, Name, _fieldSize, fieldState); ;
    }

    public bool TryToUpdateState(int x, int y)
    {
        var pos = x * FieldSize + y;
        var prevState = Field[pos];

        if (CellIsOccupied(x, y))
        {
            SetCell(x, y, CellState.empty);
            Fleet.RemoveShipDeck(x, y);
        }
        else if (!OnDiagonal(x, y) && Fleet.AddShipDeck(x, y))
        {
            SetCell(x, y, CellState.ship);
        }

        return Field[pos] != prevState;
    }

    public Dictionary<int, CellState> CheckShotResult(int x, int y)
    {
        //if (Fleet.Ships.Any(s => s.Contains())
        //    return new Dictionary<int, CellState>() { { x * 10 + y, CellState.hit } };
        //else
        //    return new Dictionary<int, CellState>() { { x * 10 + y, CellState.miss } };
        return [];
    }

    public void ClearField()
    {
        Field = new CellState[_fieldSize * _fieldSize];
        Fleet.Clear();
    }
}
