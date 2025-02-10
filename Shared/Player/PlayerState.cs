using SeaBattle.Shared.Ships;

namespace SeaBattle.Shared.Player;

/// <summary>
/// Event arguments for when a ship is destroyed
/// </summary>
public class ShipDestroyedEventArgs : EventArgs
{
    public Ship DestroyedShip { get; }
    public ShipDestroyedEventArgs(Ship ship) => DestroyedShip = ship;
}

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

    public bool Ready { get; set; } = false;

    public CellState[] Field { get; private set; }

    public Fleet Fleet { get; private set; } = new();

    public Stack<(int, int)> Shots { get; }

    /// <summary>
    /// Fired when a ship is completely destroyed
    /// </summary>
    public event EventHandler<ShipDestroyedEventArgs>? ShipDestroyed;

    #endregion

    private int CellIndex(int x, int y) => x * FieldSize + y;

    private CellState GetCell(int x, int y) => Field[CellIndex(x, y)];

    private bool CellIsOccupied(int x, int y) => x >= 0 && x < FieldSize &&
                                                 y >= 0 && y < FieldSize && GetCell(x, y) == CellState.ship;

    private void SetCell(int x, int y, CellState value) => Field[CellIndex(x, y)] = value;

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

        return new(PlayerId, Name, "", _fieldSize, fieldState); ;
    }

    public bool TryToUpdateState(int x, int y)
    {
        var pos = CellIndex(x, y);
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

    /// <summary>
    /// Processes a shot at the specified coordinates and returns the resulting cell state changes
    /// </summary>
    /// <param name="x">X coordinate of the shot</param>
    /// <param name="y">Y coordinate of the shot</param>
    /// <returns>Dictionary of affected cells and their new states, or null if shot is invalid</returns>
    public Dictionary<int, CellState>? CheckShotResult(int x, int y)
    {
        if (x < 0 || x >= FieldSize || y < 0 || y >= FieldSize)
            throw new ArgumentOutOfRangeException($"Shot coordinates ({x},{y}) are outside the field");

        // Find if any ship occupies the target coordinates
        var ship = Fleet.Ships.FirstOrDefault(s => s.Any(deck => deck.HasPosition(x, y)));

        // Case 1: Miss - no ship at coordinates
        if (ship == null)
            return new Dictionary<int, CellState>() { { CellIndex(x, y), CellState.miss } };

        // Find the specific deck (ship segment) that was hit
        var deck = ship.FirstOrDefault(d => d.HasPosition(x, y))!;

        // Case 2: Already hit - return null to indicate invalid shot
        if (deck.State == CellState.hit)
            return null;

        // Update the hit deck's state
        var updatedDeck = deck with { State = CellState.hit };
        UpdateShipDeck(ship, deck, updatedDeck);

        // Check if the entire ship is destroyed
        if (ship.All(d => d.State == CellState.hit))
        {
            Fleet.Ships.Remove(ship);
            var result = HandleShipDestruction(ship, x, y);
            ShipDestroyed?.Invoke(this, new ShipDestroyedEventArgs(ship));
            return result;
        }

        // Case 3: Hit but ship not destroyed
        return new Dictionary<int, CellState>() { { CellIndex(x, y), CellState.hit } };
    }

    /// <summary>
    /// Updates a ship's deck after being hit
    /// </summary>
    private void UpdateShipDeck(Ship ship, ShipDeck oldDeck, ShipDeck newDeck)
    {
        Field[CellIndex(oldDeck.X, oldDeck.Y)] = CellState.hit;
        ship.Remove(oldDeck);
        ship.Add(newDeck);
    }

    /// <summary>
    /// Handles the destruction of a ship and updates surrounding cells
    /// </summary>
    private Dictionary<int, CellState> HandleShipDestruction(Ship ship, int hitX, int hitY)
    {
        var result = new Dictionary<int, CellState>() { { CellIndex(hitX, hitY), CellState.hit } };
        MarkAdjacentCells(ship, result);
        return result;
    }

    /// <summary>
    /// Marks all valid adjacent cells around a ship as misses
    /// </summary>
    private void MarkAdjacentCells(Ship ship, Dictionary<int, CellState> result)
    {
        foreach (var deck in ship)
        {
            for (int i = deck.X - 1; i <= deck.X + 1; i++)
            {
                for (int j = deck.Y - 1; j <= deck.Y + 1; j++)
                {
                    if (IsValidAdjacentCell(i, j, deck.X, deck.Y))
                    {
                        MarkCellAsMiss(i, j, result);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks if a cell is valid for marking as a miss
    /// </summary>
    private bool IsValidAdjacentCell(int x, int y, int shipX, int shipY)
    {
        return x >= 0 && x < FieldSize &&
               y >= 0 && y < FieldSize &&
               !(x == shipX && y == shipY);
    }

    /// <summary>
    /// Marks a cell as a miss if it's empty and not already in the result
    /// </summary>
    private void MarkCellAsMiss(int x, int y, Dictionary<int, CellState> result)
    {
        int index = CellIndex(x, y);
        if (Field[index] == CellState.empty && !result.ContainsKey(index))
        {
            Field[index] = CellState.miss;
            result.TryAdd(index, CellState.miss);
        }
    }

    public void ClearField()
    {
        Field = new CellState[_fieldSize * _fieldSize];
        Fleet.Clear();
    }
}
