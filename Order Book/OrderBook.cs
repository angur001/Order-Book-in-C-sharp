using Order_Book.classes;
using Order_Book.enums;
using Order_Book.structs;

namespace Order_Book
{
    public class OrderBook
    {
        public struct OrderEntry
        {
            public Order order { get; set; }
            public LinkedListNode<OrderEntry> orderPtr { get; set; }
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

        public List<Trade> MatchOrders()
        {
            List<Trade> trades = new List<Trade>();
            while (true)
            {
                if (_bids.Count == 0 || _asks.Count == 0)
                {
                    break;
                }

                var bestBid = _bids.First();
                var bestAsk = _asks.First();

                var (bidPrice, bids) = bestBid;
                var (askPrice, asks) = bestAsk;

                if (bidPrice < askPrice) break;

                while (bids.Count > 0 && asks.Count > 0)
                {
                    var bid = bids.First?.Value;
                    var ask = asks.First?.Value;

                    if (bid == null || ask == null) break;
                    Quantity tradeQuantity = (Quantity)Math.Min(
                        (ulong)bid?.order.GetRemainingQuantity(), 
                        (ulong)ask?.order.GetRemainingQuantity()
                    );

                    bid?.order.Fill(tradeQuantity);
                    ask?.order.Fill(tradeQuantity);

                    // update _asks and _bids and orders
                    if (bid?.order.GetRemainingQuantity() == 0 || bid?.order.GetOrderType() == OrderType.FillAndKill)
                    {
                        bids.Remove(bid?.orderPtr!);
                        if (bids.Count == 0)
                        {
                            _bids.Remove(bid?.order.GetPrice() ?? throw new InvalidOperationException("Price is null."));
                        }
                        _orders.Remove(bid?.order.GetOrderId() ?? throw new InvalidOperationException("Order ID is null."));
                    }
                    if (ask?.order.GetRemainingQuantity() == 0 || ask?.order.GetOrderType() == OrderType.FillAndKill)
                    {
                        asks.Remove(ask?.orderPtr!);
                        if (asks.Count == 0)
                        {
                            _asks.Remove(ask?.order.GetPrice() ?? throw new InvalidOperationException("Price is null."));
                        }
                        _orders.Remove(ask?.order.GetOrderId() ?? throw new InvalidOperationException("Order ID is null."));
                    }
                    
                    // create Trade Obejct
                    trades.Add(new Trade(
                        new TradeInfo(
                            bid?.order.GetOrderId() ?? throw new InvalidOperationException("Order ID is null."),
                            bid?.order.GetPrice() ?? throw new InvalidOperationException("Price is null."),
                            tradeQuantity),
                        new TradeInfo(
                            ask?.order.GetOrderId() ?? throw new InvalidOperationException("Order ID is null."),
                            ask?.order.GetPrice() ?? throw new InvalidOperationException("Price is null."),
                            tradeQuantity)
                    ));

                }
            }
            return trades;
        }
    }
}