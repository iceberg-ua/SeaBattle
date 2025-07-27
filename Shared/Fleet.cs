using SeaBattle.Shared.Ships;

namespace SeaBattle.Shared;

public class Fleet
{
    private readonly int _maxShipSize = 4;

    public List<Ship> Ships { get; } = [];

    public bool Complete { get; private set; }

    private bool CheckCompletion()
    {
        if(Ships.Count != 10)
            return false;

        foreach (var group in Ships.GroupBy(s => s.Count))
        {
            if ((5 - group.Count()) != group.First().Count)
                return false;
        }

        return true;
    }

    public bool AddShipDeck(int x, int y)
    {
        if (Complete)
            return false;

        var existingShips = Ships.Where(ship => ship.Any(s => s.HasPosition(x - 1, y)) ||
                                                ship.Any(s => s.HasPosition(x + 1, y)) ||
                                                ship.Any(s => s.HasPosition(x, y - 1)) ||
                                                ship.Any(s => s.HasPosition(x, y + 1))).ToList();
        var count = existingShips.Count;

        if (count > 1)
        {
            var newShipSize = existingShips.Sum(s => s.Size) + 1;
            if (newShipSize > _maxShipSize)
                return false;

            // Check if we can convert these ships to a new size
            if (!CanConvertToShipOfSize(newShipSize, existingShips))
                return false;

            Ships.RemoveAll(existingShips.Contains);
            var newShip = new Ship();

            foreach (var item in existingShips)
            {
                newShip.AddRange(item);
            }

            newShip.AddDeck(new(x, y, CellState.ship));
            Ships.Add(newShip);
        }
        else if (count == 1)
        {
            var ship = existingShips.First();

            if (ship.Size == _maxShipSize)
                return false;

            var newShipSize = ship.Size + 1;
            // Check if we can convert this ship to a new size
            if (!CanConvertToShipOfSize(newShipSize, [ship]))
                return false;

            ship.AddDeck(new(x, y, CellState.ship));
        }
        else
        {
            // Adding a new single-deck ship
            if (!CanAddShipOfSize(1))
                return false;

            Ships.Add([new(x, y, CellState.ship)]);
        }

        Complete = CheckCompletion();

        return true;
    }

    public void RemoveShipDeck(int x, int y)
    {
        var existingShips = Ships.Where(s => s.Any(s => s.HasPosition(x, y))).ToList();

        if (existingShips.Count == 0)
            return;

        var owner = existingShips.First();
        var index = owner.FindIndex(s => s.HasPosition(x,y));

        var firstPart = owner[..index];
        var secondPart = owner[(index + 1)..];

        Ships.Remove(owner);

        if (firstPart.Count > 0)
            Ships.Add(new Ship(firstPart));

        if (secondPart.Count > 0)
            Ships.Add(new Ship(secondPart));

        Complete = CheckCompletion();
    }

    public void Clear()
    {
        Ships.Clear();
        Complete = false;
    }

    /// <summary>
    /// Gets the current count of ships by size
    /// </summary>
    public Dictionary<int, int> GetShipCounts()
    {
        var counts = new Dictionary<int, int>
        {
            [1] = 0,
            [2] = 0,
            [3] = 0,
            [4] = 0
        };

        foreach (var ship in Ships)
        {
            if (counts.ContainsKey(ship.Size))
                counts[ship.Size]++;
        }

        return counts;
    }

    /// <summary>
    /// Gets the maximum allowed ships by size
    /// </summary>
    public static Dictionary<int, int> GetMaxShipCounts()
    {
        return new Dictionary<int, int>
        {
            [1] = 4, // 4 single-deck ships
            [2] = 3, // 3 two-deck ships  
            [3] = 2, // 2 three-deck ships
            [4] = 1  // 1 four-deck ship
        };
    }

    /// <summary>
    /// Checks if we can add a ship of the specified size without exceeding limits
    /// </summary>
    private bool CanAddShipOfSize(int size)
    {
        var currentCounts = GetShipCounts();
        var maxCounts = GetMaxShipCounts();

        if (!maxCounts.ContainsKey(size))
            return false;

        return currentCounts[size] < maxCounts[size];
    }

    /// <summary>
    /// Checks if we can convert ships to a new size (accounts for removing existing ships)
    /// </summary>
    private bool CanConvertToShipOfSize(int newSize, List<Ship> shipsToRemove)
    {
        var currentCounts = GetShipCounts();
        var maxCounts = GetMaxShipCounts();

        if (!maxCounts.ContainsKey(newSize))
            return false;

        // Account for ships that will be removed in the conversion
        foreach (var ship in shipsToRemove)
        {
            if (currentCounts.ContainsKey(ship.Size))
                currentCounts[ship.Size]--;
        }

        return currentCounts[newSize] < maxCounts[newSize];
    }
}
