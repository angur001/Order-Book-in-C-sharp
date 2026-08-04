using System.Collections.Concurrent;
using OrderBook.Classes;
using OrderBook.Enums;
using OrderBook.Interfaces;
using OrderBook.OrderCommands;
using OrderBook.Structs;
using TradeNamespace = OrderBook.Trade;

namespace OrderBook;

public class OrderBook : IDisposable
{
    private readonly SortedDictionary<Price, LinkedList<Order>> _asks =
        new SortedDictionary<Price, LinkedList<Order>>(Comparer<Price>.Create((x, y) => x.Value.CompareTo(y.Value)));
    private readonly SortedDictionary<Price, LinkedList<Order>> _bids =
        new SortedDictionary<Price, LinkedList<Order>>(Comparer<Price>.Create((x, y) => y.Value.CompareTo(x.Value)));
    private readonly ConcurrentDictionary<OrderId, LinkedListNode<Order>> _orders = new();

    // Guards mutations of _asks/_bids (SortedDictionary/LinkedList are not thread-safe).
    // Monitor locks are reentrant on the same thread, so nested lock statements
    // (AddOrder -> MatchOrders, or the pruner batching CancelOrder calls) are safe.
    private readonly object _mutex = new();

    // Drives the GoodForDay pruning: cancelled to wake the thread early on Dispose.
    private readonly CancellationTokenSource _pruneCts = new();
    private readonly Thread _pruneThread;

    private readonly ConcurrentDictionary<Price, LevelData> _metadata = new();

    public OrderBook()
    {
        _pruneThread = new Thread(PruneGoodForDayOrdersLoop)
        {
            IsBackground = true,
            Name = "GoodForDayOrderPruner"
        };
        _pruneThread.Start();
    }

    public bool CanMatch(Side side, Price price)
    {
        lock (_mutex)
        {
            if (side == Side.Buy)
            {
                return _asks.Count > 0 && price.Value >= _asks.First().Key.Value;
            }

            return _bids.Count > 0 && price.Value <= _bids.First().Key.Value;
        }
    }

    public bool CanFullyMatch(Side side, Price price, Quantity quantity)
    {
        lock (_mutex)
        {
            if (!CanMatch(side, price)) return false;
            Price threshold;

            if (side == Side.Buy)
            {
                threshold = _asks.First().Key;
            }
            else
            {
                threshold = _bids.First().Key;
            }

            foreach ((var levelPrice, var levelData) in _metadata)
            {
                if ((side == Side.Buy && levelPrice.Value < threshold.Value) ||
                    (side == Side.Sell && levelPrice.Value > threshold.Value))
                {
                    continue;
                }

                if ((side == Side.Buy && levelPrice.Value > price.Value) ||
                    (side == Side.Sell && levelPrice.Value < price.Value))
                {
                    continue;
                }

                if (levelData.quantity.Value >= quantity.Value)
                {
                    return true;
                }

                quantity -= levelData.quantity;
            }

            return false;
        }
    }

    public List<TradeNamespace.Trade> MatchOrders()
    {
        lock (_mutex)
        {
            List<TradeNamespace.Trade> trades = new();
            while (true)
            {
                // if there is no bid or ask, we cannot match any orders, so we break the loop
                if (_bids.Count == 0 || _asks.Count == 0)
                {
                    break;
                }

                // get the best bid and ask prices and their corresponding order lists
                var bestBid = _bids.First();
                var bestAsk = _asks.First();

                var (bidPrice, bids) = bestBid;
                var (askPrice, asks) = bestAsk;

                // if the best bid price is lower than the best ask price, we cannot match any orders, so we break the loop
                if (bidPrice.Value < askPrice.Value) break;

                // we loop through the orders at the best bid and ask prices and match them until one of the lists is empty
                while (bids.Count > 0 && asks.Count > 0)
                {
                    var bid = bids.First?.Value;
                    var ask = asks.First?.Value;

                    if (bid == null || ask == null) break;

                    Quantity tradeQuantity = new(Math.Min(
                        bid.GetRemainingQuantity().Value,
                        ask.GetRemainingQuantity().Value
                    ));

                    bid.Fill(tradeQuantity);
                    ask.Fill(tradeQuantity);

                    if (bid.GetRemainingQuantity().Value == 0)
                    {
                        bids.RemoveFirst();
                        _orders.TryRemove(bid.GetOrderId(), out _);
                    }
                    if (ask.GetRemainingQuantity().Value == 0)
                    {
                        asks.RemoveFirst();
                        _orders.TryRemove(ask.GetOrderId(), out _);
                    }

                    trades.Add(new TradeNamespace.Trade(
                        new TradeInfo(
                            bid.GetOrderId(),
                            bid.GetPrice(),
                            tradeQuantity),
                        new TradeInfo(
                            ask.GetOrderId(),
                            ask.GetPrice(),
                            tradeQuantity)
                    ));

                    // Update LevelData for the matched price level
                    OnOrderMatched(bidPrice, tradeQuantity, bid.IsFilled());
                    OnOrderMatched(askPrice, tradeQuantity, ask.IsFilled());
                }

                // Remove the price level from the tree if its list is now empty. This must happen
                // before the next outer-loop iteration, otherwise _bids.First()/_asks.First() would
                // keep re-selecting this exhausted (but still-present) level forever.
                if (bids.Count == 0)
                {
                    _bids.Remove(bidPrice);
                }

                if (asks.Count == 0)
                {
                    _asks.Remove(askPrice);
                }
            }

            // Remove All Remaining Orders with FillAndKill OrderType
            if (_bids.Count > 0)
            {
                var (_, remainingBids) = _bids.First();
                var order = remainingBids.First?.Value;
                if (order != null && order.GetOrderType() == OrderType.FillAndKill)
                {
                    CancelOrder(order.GetOrderId());
                }
            }

            if (_asks.Count > 0)
            {
                var (_, remainingAsks) = _asks.First();
                var order = remainingAsks.First?.Value;
                if (order != null && order.GetOrderType() == OrderType.FillAndKill)
                {
                    CancelOrder(order.GetOrderId());
                }
            }

            return trades;
        }
    }

    private void OnOrderMatched(Price bidPrice, Quantity tradeQuantity, bool isFullyFilled)
    {
        UpdateLevelData(bidPrice, tradeQuantity, isFullyFilled ? LevelData.Action.Remove : LevelData.Action.Match);
    }

    public List<TradeNamespace.Trade> AddOrder(Order order)
    {
        lock (_mutex)
        {
            // if order already exists, throw an exception
            if (_orders.ContainsKey(order.GetOrderId()))
            {
                return new List<TradeNamespace.Trade>();
            }

            if (order.GetOrderType() == OrderType.Market)
            {
                if (order.GetSide() == Side.Buy && _asks.Count != 0)
                {
                    var (worstAskPrice, _) = _asks.Last();
                    order = order.ToGoodTillCancel(worstAskPrice);
                }
                else if (order.GetSide() == Side.Sell && _bids.Count != 0)
                {
                    var (worstBidPrice, _) = _bids.Last();
                    order = order.ToGoodTillCancel(worstBidPrice);
                }
            }

            // if the order is a FillAndKill order and cannot be matched 
            if (order.GetOrderType() == OrderType.FillAndKill && !CanMatch(order.GetSide(), order.GetPrice()))
            {
                return new List<TradeNamespace.Trade>();
            }

            // if the order is a FillOrKill order and cannot be fully matched, throw an exception
            if (order.GetOrderType() == OrderType.FillOrKill && !CanFullyMatch(order.GetSide(), order.GetPrice(), order.GetInitialQuantity()))
            {
                return new List<TradeNamespace.Trade>();
            }

            // Add the order to the appropriate side of the order book
            LinkedListNode<Order> orderNode;
            if (order.GetSide() == Side.Buy)
            {
                if (!_bids.TryGetValue(order.GetPrice(), out var bids))
                {
                    bids = new LinkedList<Order>();
                    _bids[order.GetPrice()] = bids;
                }

                orderNode = bids.AddLast(order);
            }
            else
            {
                if (!_asks.TryGetValue(order.GetPrice(), out var asks))
                {
                    asks = new LinkedList<Order>();
                    _asks[order.GetPrice()] = asks;
                }

                orderNode = asks.AddLast(order);
            }

            // Add the order to the dictionary of orders
            _orders[order.GetOrderId()] = orderNode;

            OnOrderAdded(order);

            return MatchOrders();
        }
    }

    private void OnOrderAdded(Order order)
    {
        UpdateLevelData(order.GetPrice(), order.GetRemainingQuantity(), LevelData.Action.Add);
    }

    private void UpdateLevelData(Price price, Quantity quantity, LevelData.Action action)
    {
        Quantity newQuantity;
        Quantity newCount;
        if (_metadata.TryGetValue(price, out var levelData))
        {
            if (action == LevelData.Action.Add)
            {
                newQuantity = levelData.quantity + quantity;
                newCount = levelData.count + new Quantity(1);
            }
            else if (action == LevelData.Action.Remove)
            {
                newQuantity = levelData.quantity - quantity;
                newCount = levelData.count - new Quantity(1);
            }
            else // (action == LevelData.Action.Match)
            {
                newQuantity = levelData.quantity - quantity;;
                newCount = levelData.count;
            }
        }
        else
        {
            // First order ever seen at this price level: Add is the only action
            // that should be able to reach this branch (Remove/Match imply the
            // level already existed).
            newQuantity = quantity;
            newCount = new Quantity(1);
        }

        if (newCount == new Quantity(0))
        {
            _metadata.TryRemove(price, out _);
        }
        else
        {
            _metadata[price] = new LevelData { quantity = newQuantity, count = newCount };
        }
    }

    public void CancelOrder(OrderId orderId)
    {
        lock (_mutex)
        {
            _orders.TryGetValue(orderId, out var orderNode);
            if (orderNode == null) return;

            var order = orderNode.Value;
            if (order.GetSide() == Side.Buy)
            {
                if (_bids.TryGetValue(order.GetPrice(), out var bids))
                {
                    bids.Remove(orderNode);
                    if (bids.Count == 0)
                    {
                        _bids.Remove(order.GetPrice());
                    }
                }
            }
            else
            {
                if (_asks.TryGetValue(order.GetPrice(), out var asks))
                {
                    asks.Remove(orderNode);
                    if (asks.Count == 0)
                    {
                        _asks.Remove(order.GetPrice());
                    }
                }
            }

            _orders.TryRemove(orderId, out _);

            OnOrderCancelled(order);
        }
    }

    private void OnOrderCancelled(Order order)
    {
        UpdateLevelData(order.GetPrice(), order.GetRemainingQuantity(), LevelData.Action.Remove);
    }

    public List<TradeNamespace.Trade> ModifyOrder(ModifyOrderCommand modifyOrderCommand)
    {
        lock (_mutex)
        {
            if (!_orders.TryGetValue(modifyOrderCommand.GetOrderId(), out var orderNode))
            {
                return new List<TradeNamespace.Trade>();
            }

            var order = orderNode.Value;
            CancelOrder(order.GetOrderId());
            return AddOrder(modifyOrderCommand.ToOrder(order.GetOrderType()));
        }
    }

    public int GetOrderCount()
    {
        return _orders.Count;
    }

    public IOrderBookTicksInfos GetOrderBookTickInfos()
    {
        lock (_mutex)
        {
            List<Tick> asks = new();
            List<Tick> bids = new();

            foreach (var (price, orders) in _asks)
            {
                Quantity totalQuantity = new((uint)orders.Sum(order => order.GetRemainingQuantity().Value));
                asks.Add(new Tick { price = price, quantity = totalQuantity });
            }

            foreach (var (price, orders) in _bids)
            {
                Quantity totalQuantity = new((uint)orders.Sum(order => order.GetRemainingQuantity().Value));
                bids.Add(new Tick { price = price, quantity = totalQuantity });
            }

            return new OrderBookTickInfos(asks, bids);
        }
    }

    /// Background loop that wakes up exactly at each upcoming midnight and prunes
    /// any GoodForDay orders still resting in the book.
    private void PruneGoodForDayOrdersLoop()
    {
        var token = _pruneCts.Token;
        while (!token.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextMidnight = now.Date.AddDays(1);
            var delay = nextMidnight - now;

            // WaitOne returns true only if the token's wait handle was signalled (Dispose was called),
            // in which case we stop; a timeout means it's midnight and we should prune.
            if (token.WaitHandle.WaitOne(delay))
            {
                break;
            }

            PruneGoodForDayOrders();
        }
    }

    private void PruneGoodForDayOrders()
    {
        // Lock-free scan: _orders is a ConcurrentDictionary, so enumerating it while
        // other threads add/remove entries is safe and doesn't require the mutex.
        // Only orders actually found are cancelled
        List<OrderId>? goodForDayOrderIds = null;
        foreach (var (orderId, node) in _orders)
        {
            if (node.Value.GetOrderType() != OrderType.GoodForDay) continue;
            (goodForDayOrderIds ??= new List<OrderId>()).Add(orderId);
        }

        if (goodForDayOrderIds == null) return;

        // mutation is batched
        // under a single lock acquisition instead of one per order.
        // cool trick
        lock (_mutex)
        {
            foreach (var orderId in goodForDayOrderIds)
            {
                CancelOrder(orderId);
            }
        }
    }

    public void Dispose()
    {
        _pruneCts.Cancel();
        _pruneThread.Join();
        _pruneCts.Dispose();
    }
}