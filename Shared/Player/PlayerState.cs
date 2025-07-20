using SeaBattle.Shared.Ships;

namespace SeaBattle.Shared.Player;

public class PlayerState
{
    public PlayerState(string name, Guid gameId, int fieldSize)
    {
        FieldSize = fieldSize;

        Name = name;
        GameId = gameId;
        PlayerId = Guid.NewGuid();

        Field = new CellState[FieldSize * FieldSize];
        Shots = new(FieldSize * FieldSize);
        EnemyFieldState = new CellState[FieldSize * FieldSize];
    }

    #region Properties

    public Guid GameId { get; }

    public Guid PlayerId { get; set; }

    public string Name { get; }

    public int FieldSize { get; }

    public bool Ready => State == PlayerStateEnum.Ready;

    public PlayerStateEnum State { get; private set; } = PlayerStateEnum.Formation;

    public CellState[] Field { get; private set; }

    public Fleet Fleet { get; } = new();

    public Stack<(int, int)> Shots { get; }
    
    /// <summary>
    /// Optimized shot tracking: stores the revealed enemy field state directly
    /// instead of reconstructing it from shot coordinates every time.
    /// </summary>
    public CellState[] EnemyFieldState { get; private set; }

    #endregion
    
    #region Public methods
    
    public PlayerInfo GetPlayerInfo()
    {
        var fieldState = Fleet.Ships.SelectMany(ship => ship).ToDictionary(deck => deck.X * FieldSize + deck.Y, deck => deck.State);

        return new(PlayerId, Name, State, fieldState);
    }

    public void SetReady()
    {
        State = PlayerStateEnum.Ready;
    }

    public void SetInTurn()
    {
        State = PlayerStateEnum.InTurn;
    }

    public void SetWaitingForTurn()
    {
        State = PlayerStateEnum.WaitingForTurn;
    }

    public void SetWon()
    {
        State = PlayerStateEnum.Won;
    }

    public void SetLost()
    {
        State = PlayerStateEnum.Lost;
    }

    /// <summary>
    /// Attempts to update the state of the cell at the specified coordinates.
    /// If the cell is occupied by a ship, it will be cleared. If the cell is empty and not on a diagonal,
    /// a ship deck will be added to the cell.
    /// This method is called only on the formation stage.
    /// </summary>
    /// <param name="x">The X coordinate of the cell.</param>
    /// <param name="y">The Y coordinate of the cell.</param>
    /// <returns>True if the cell state was changed, otherwise false.</returns>
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
    /// Processes a shot at the specified coordinates and returns the resulting cell state changes.
    /// This method is called during the game stage.
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
            return result;
        }

        // Case 3: Hit but ship not destroyed
        return new Dictionary<int, CellState>() { { CellIndex(x, y), CellState.hit } };
    }
    
    /// <summary>
    /// Records a shot result directly to the enemy field state for optimized access.
    /// Maintains both the sequential shot history AND the optimized field cache.
    /// </summary>
    /// <param name="x">X coordinate of the shot</param>
    /// <param name="y">Y coordinate of the shot</param>
    /// <param name="result">The result of the shot (hit, miss, etc.)</param>
    public void RecordShotResult(int x, int y, CellState result)
    {
        // Maintain sequential shot history for game replay/undo functionality
        Shots.Push((x, y));
        
        // Update optimized enemy field cache for fast access
        var index = x * FieldSize + y;
        if (index >= 0 && index < EnemyFieldState.Length)
        {
            EnemyFieldState[index] = result;
        }
    }

    /// <summary>
    /// Records multiple shot results at once (for when a ship is destroyed and adjacent cells are revealed).
    /// Maintains shot sequence by only recording the actual player shot, not the revealed adjacent cells.
    /// </summary>
    /// <param name="shotResults">Dictionary of cell indices and their states</param>
    /// <param name="actualShotX">X coordinate of the actual shot (not adjacent reveals)</param>
    /// <param name="actualShotY">Y coordinate of the actual shot (not adjacent reveals)</param>
    public void RecordMultipleShotResults(Dictionary<int, CellState> shotResults, int actualShotX, int actualShotY)
    {
        // Maintain sequential shot history for the actual shot only
        Shots.Push((actualShotX, actualShotY));
        
        // Update optimized enemy field cache for all revealed cells
        foreach (var (index, state) in shotResults)
        {
            if (index >= 0 && index < EnemyFieldState.Length)
            {
                EnemyFieldState[index] = state;
            }
        }
    }

    /// <summary>
    /// Gets the sequential shot history as a list (oldest to newest).
    /// Useful for game replay, analysis, or displaying move history.
    /// </summary>
    /// <returns>List of shots in chronological order</returns>
    public List<(int X, int Y)> GetShotHistory()
    {
        return Shots.Reverse().ToList();
    }

    /// <summary>
    /// Gets the last N shots from history.
    /// Useful for showing recent moves or implementing undo functionality.
    /// </summary>
    /// <param name="count">Number of recent shots to retrieve</param>
    /// <returns>List of recent shots (newest first)</returns>
    public List<(int X, int Y)> GetRecentShots(int count)
    {
        return Shots.Take(count).ToList();
    }

    /// <summary>
    /// Checks if a specific coordinate has been shot at.
    /// Uses the optimized cache for O(1) lookup instead of iterating through shot history.
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <returns>True if this coordinate has been shot at</returns>
    public bool HasShotAt(int x, int y)
    {
        var index = x * FieldSize + y;
        return index >= 0 && index < EnemyFieldState.Length && EnemyFieldState[index] != CellState.empty;
    }

    /// <summary>
    /// Rebuilds the enemy field cache from shot history.
    /// Used for data consistency checks or after undo operations.
    /// </summary>
    /// <param name="opponentField">The opponent's actual field to reference</param>
    public void RebuildEnemyFieldFromHistory(CellState[] opponentField)
    {
        // Clear the cache
        EnemyFieldState = new CellState[FieldSize * FieldSize];
        
        // Rebuild from shot history (this is the O(n) operation we normally avoid)
        foreach (var (x, y) in Shots)
        {
            var index = x * FieldSize + y;
            if (index >= 0 && index < EnemyFieldState.Length)
            {
                EnemyFieldState[index] = opponentField[index];
            }
        }
    }

    ///<summary>
    /// Clears the player's field and fleet.
    /// Resets the field to an empty state and clears all ships from the fleet.
    /// </summary>
    public void ClearField()
    {
        Field = new CellState[FieldSize * FieldSize];
        Fleet.Clear();
        // Also clear enemy field state when clearing
        EnemyFieldState = new CellState[FieldSize * FieldSize];
    }
    
    #endregion
    
    #region Private methods

    private CellState GetCell(int x, int y) => Field[CellIndex(x, y)];

    private void SetCell(int x, int y, CellState value) => Field[CellIndex(x, y)] = value;
    
    private int CellIndex(int x, int y) => x * FieldSize + y;

    private bool CellIsOccupied(int x, int y) => x >= 0 && x < FieldSize &&
                                                 y >= 0 && y < FieldSize && GetCell(x, y) == CellState.ship;

    private bool OnDiagonal(int x, int y) => CellIsOccupied(x - 1, y - 1) || CellIsOccupied(x + 1, y - 1) ||
                                             CellIsOccupied(x - 1, y + 1) || CellIsOccupied(x + 1, y + 1);

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
    
    #endregion
}
