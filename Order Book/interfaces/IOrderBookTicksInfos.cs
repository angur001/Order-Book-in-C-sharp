using Order_Book.structs;

namespace Order_Book.interfaces
{
public interface IOrderBookTicksInfos
    {
        IEnumerable<Tick> GetTicks();
        IEnumerable<Tick> GetAsks();
        IEnumerable<Tick> GetBids();
    }
}