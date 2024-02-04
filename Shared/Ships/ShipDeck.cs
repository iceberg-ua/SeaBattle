namespace SeaBattle.Shared.Ships;

public record ShipDeck(int X, int Y, CellState State)
{
    public bool HasPosition(int x, int y) => x == X && y == Y;
}