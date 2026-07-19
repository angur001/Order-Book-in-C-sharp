using Order_Book.classes;
using Order_Book.enums;

namespace Order_Book
{
    public class OrderBook
    {
        private struct OrderEntry
        {
            public Order order { get; set; }
            public LinkedListNode<OrderEntry> OrderPtr { get; set; }
        }

        private readonly SortedDictionary<Price, LinkedList<OrderEntry>> _asks = new();
        private readonly SortedDictionary<Price, LinkedList<OrderEntry>> _bids = 
            new SortedDictionary<decimal, LinkedList<OrderEntry>>(Comparer<decimal>.Create((x, y) => y.CompareTo(x)));
        private readonly Dictionary<orderId, OrderEntry> _orders = new();

        public bool CanMatch(Side side, Price price)
        {
            if (side == Side.Buy)
            {
                return _asks.Count > 0 && price >= _asks.First().Key;
            }
            else
            {
                return _bids.Count > 0 && price <= _bids.First().Key;
            }
        }


    }
}