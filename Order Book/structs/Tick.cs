using OrderBook;

namespace OrderBook.Structs;

public struct Tick
{
    public Price price { get; set; }
    public Quantity quantity { get; set; }
}