﻿using SeaBattle.Shared.Ships;

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
            if (existingShips.Sum(s => s.Size) + 1 > _maxShipSize)
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

            ship.AddDeck(new(x, y, CellState.ship));
        }
        else
            Ships.Add([new(x, y, CellState.ship)]);

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
}
