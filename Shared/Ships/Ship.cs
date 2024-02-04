namespace SeaBattle.Shared.Ships;

public class Ship : List<ShipDeck>
{
    public Ship() { }

    public Ship(IEnumerable<ShipDeck> items)
        : base(items) { }

    public int Size => Count;

    public void AddDeck(ShipDeck deck)
    {
        Add(deck);
        var ordered = this.OrderBy(s => s.X).ThenBy(s => s.Y).ToList();
        Clear();
        AddRange(ordered);
    }
}