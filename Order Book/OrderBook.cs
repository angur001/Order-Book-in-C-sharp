using OrderBook.Classes;
using OrderBook.Enums;
using OrderBook.Structs;
using TradeNamespace = OrderBook.Trade;

namespace OrderBook;

public class OrderBook
{
    public struct OrderEntry
    {
        public Order order { get; set; }
        public LinkedListNode<OrderEntry> orderPtr { get; set; }
    }

    private readonly SortedDictionary<Price, LinkedList<OrderEntry>> _asks = new();
    private readonly SortedDictionary<Price, LinkedList<OrderEntry>> _bids =
        new SortedDictionary<Price, LinkedList<OrderEntry>>(Comparer<Price>.Create((x, y) => y.Value.CompareTo(x.Value)));
    private readonly Dictionary<OrderId, OrderEntry> _orders = new();

    public bool CanMatch(Side side, Price price)
    {
        if (side == Side.Buy)
        {
            return _asks.Count > 0 && price.Value >= _asks.First().Key.Value;
        }

        return _bids.Count > 0 && price.Value <= _bids.First().Key.Value;
    }

    public List<TradeNamespace.Trade> MatchOrders()
    {
        List<TradeNamespace.Trade> trades = new();
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

            if (bidPrice.Value < askPrice.Value) break;

            while (bids.Count > 0 && asks.Count > 0)
            {
                var bidEntry = bids.First?.Value;
                var askEntry = asks.First?.Value;

                if (bidEntry == null || askEntry == null) break;

                var bid = bidEntry.Value;
                var ask = askEntry.Value;

                Quantity tradeQuantity = new(Math.Min(
                    bid.order.GetRemainingQuantity().Value,
                    ask.order.GetRemainingQuantity().Value
                ));

                bid.order.Fill(tradeQuantity);
                ask.order.Fill(tradeQuantity);

                if (bid.order.GetRemainingQuantity().Value == 0 || bid.order.GetOrderType() == OrderType.FillAndKill)
                {
                    bids.Remove(bid.orderPtr!);
                    if (bids.Count == 0)
                    {
                        _bids.Remove(bid.order.GetPrice());
                    }
                    _orders.Remove(bid.order.GetOrderId());
                }
                if (ask.order.GetRemainingQuantity().Value == 0 || ask.order.GetOrderType() == OrderType.FillAndKill)
                {
                    asks.Remove(ask.orderPtr!);
                    if (asks.Count == 0)
                    {
                        _asks.Remove(ask.order.GetPrice());
                    }
                    _orders.Remove(ask.order.GetOrderId());
                }

                trades.Add(new TradeNamespace.Trade(
                    new TradeInfo(
                        bid.order.GetOrderId(),
                        bid.order.GetPrice(),
                        tradeQuantity),
                    new TradeInfo(
                        ask.order.GetOrderId(),
                        ask.order.GetPrice(),
                        tradeQuantity)
                ));
            }
        }
        return trades;
    }
}