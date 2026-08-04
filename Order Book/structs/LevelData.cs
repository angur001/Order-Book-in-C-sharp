namespace OrderBook.Structs;

public struct LevelData
{
    public Quantity quantity { get; set; }
    public Quantity count { get; set; }

    public enum Action
    {
        Add,
        Remove,
        Match
    }

}