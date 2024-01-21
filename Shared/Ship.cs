namespace SeaBattle.Shared;

public class Ship : List<(int x,int y)>
{
    public Ship() { }

    public Ship(IEnumerable<(int x,int y)> items)
        : base(items) { }

    public int Size => Count;

    public void AddDeck((int x, int y) deck)
    {
        Add(deck);
        var ordered = this.OrderBy(s => s.x).ThenBy(s => s.y).ToList();
        Clear();
        AddRange(ordered);
    }
}