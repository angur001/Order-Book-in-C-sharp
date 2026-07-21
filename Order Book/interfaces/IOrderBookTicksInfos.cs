using OrderBook.Structs;

namespace OrderBook.Interfaces;

public interface IOrderBookTicksInfos
{
    IEnumerable<Tick> GetTicks();
    IEnumerable<Tick> GetAsks();
    IEnumerable<Tick> GetBids();
}