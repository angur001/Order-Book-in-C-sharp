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
    // The default strategy (Strategy pattern): a tree-based ladder that works
    // for any price. Swap this at construction time - e.g. for a future
    // tick-indexed array/ring-buffer ladder - without OrderBook itself changing.
    private static readonly Func<IComparer<Price>, IPriceLadder> DefaultPriceLadderFactory =
        comparer => new TreePriceLadder(comparer);

    private readonly IPriceLadder _asks;
    private readonly IPriceLadder _bids;
    private readonly ConcurrentDictionary<OrderId, LinkedListNode<Order>> _orders = new();

    // Guards mutations of _asks/_bids (the ladder implementations are not
    // thread-safe). Monitor locks are reentrant on the same thread, so nested
    // lock statements (AddOrder -> MatchOrders, or the pruner batching
    // CancelOrder calls) are safe.
    private readonly object _mutex = new();

    // Drives the GoodForDay pruning. Routing "what time is it" and "wait until
    // this instant" through TimeProvider (instead of DateTime.Now / a raw
    // Thread.Sleep-style wait) means tests can inject a FakeTimeProvider and
    // fast-forward past a simulated midnight instead of waiting on the real
    // wall clock.
    private readonly TimeProvider _timeProvider;
    private readonly ITimer _pruneTimer;

    public OrderBook() : this(TimeProvider.System, DefaultPriceLadderFactory)
    {
    }

    public OrderBook(TimeProvider timeProvider) : this(timeProvider, DefaultPriceLadderFactory)
    {
    }

    // `priceLadderFactory` is handed a "best-first" comparer for each side and
    // must return a ladder ordered by it; it's called once per side, so the
    // same factory/strategy is used for both bids and asks.
    public OrderBook(TimeProvider timeProvider, Func<IComparer<Price>, IPriceLadder> priceLadderFactory)
    {
        _timeProvider = timeProvider;
        _asks = priceLadderFactory(Comparer<Price>.Create((x, y) => x.Value.CompareTo(y.Value)));
        _bids = priceLadderFactory(Comparer<Price>.Create((x, y) => y.Value.CompareTo(x.Value)));
        _pruneTimer = _timeProvider.CreateTimer(
            _ => PruneGoodForDayOrdersAndScheduleNext(),
            null,
            GetDelayUntilNextMidnight(),
            Timeout.InfiniteTimeSpan);
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

            // The opposite side's levels are already isolated by side and sorted
            // from best to worst, so we can walk them in order and stop as soon as
            // a level is priced beyond what the incoming order is willing to accept.
            var levels = side == Side.Buy ? _asks : _bids;
            foreach (var (levelPrice, level) in levels)
            {
                if ((side == Side.Buy && levelPrice.Value > price.Value) ||
                    (side == Side.Sell && levelPrice.Value < price.Value))
                {
                    break;
                }

                if (level.TotalQuantity.Value >= quantity.Value)
                {
                    return true;
                }

                quantity -= level.TotalQuantity;
            }

            return false;
        }
    }

    public IReadOnlyList<TradeNamespace.Trade> MatchOrders()
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

                // get the best bid and ask prices and their corresponding price levels
                var bestBid = _bids.First();
                var bestAsk = _asks.First();

                var (bidPrice, bidLevel) = bestBid;
                var (askPrice, askLevel) = bestAsk;

                // if the best bid price is lower than the best ask price, we cannot match any orders, so we break the loop
                if (bidPrice.Value < askPrice.Value) break;

                var bids = bidLevel.Orders;
                var asks = askLevel.Orders;

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

                    // Update each level's running quantity for the matched price level
                    bidLevel.RecordFill(tradeQuantity);
                    askLevel.RecordFill(tradeQuantity);
                }

                // Remove the price level from the tree if its list is now empty. This must happen
                // before the next outer-loop iteration, otherwise _bids.First()/_asks.First() would
                // keep re-selecting this exhausted (but still-present) level forever.
                _bids.RemoveLevelIfEmpty(bidPrice, bidLevel);
                _asks.RemoveLevelIfEmpty(askPrice, askLevel);
            }

            // Remove All Remaining Orders with FillAndKill OrderType
            if (_bids.Count > 0)
            {
                var (_, remainingBidLevel) = _bids.First();
                var order = remainingBidLevel.Orders.First?.Value;
                if (order != null && order.GetOrderType() == OrderType.FillAndKill)
                {
                    CancelOrder(order.GetOrderId());
                }
            }

            if (_asks.Count > 0)
            {
                var (_, remainingAskLevel) = _asks.First();
                var order = remainingAskLevel.Orders.First?.Value;
                if (order != null && order.GetOrderType() == OrderType.FillAndKill)
                {
                    CancelOrder(order.GetOrderId());
                }
            }

            return trades;
        }
    }

    public IReadOnlyList<TradeNamespace.Trade> AddOrder(Order order)
    {
        lock (_mutex)
        {
            // if order already exists, ignore this add
            if (_orders.ContainsKey(order.GetOrderId()))
            {
                return Array.Empty<TradeNamespace.Trade>();
            }

            if (order.GetOrderType() == OrderType.Market)
            {
                if (order.GetSide() == Side.Buy && _asks.WorstPrice is { } worstAskPrice)
                {
                    order = order.ToGoodTillCancel(worstAskPrice);
                }
                else if (order.GetSide() == Side.Sell && _bids.WorstPrice is { } worstBidPrice)
                {
                    order = order.ToGoodTillCancel(worstBidPrice);
                }
            }

            // if the order is a FillAndKill order and cannot be matched
            if (order.GetOrderType() == OrderType.FillAndKill && !CanMatch(order.GetSide(), order.GetPrice()))
            {
                return Array.Empty<TradeNamespace.Trade>();
            }

            // if the order is a FillOrKill order and cannot be fully matched, reject it
            if (order.GetOrderType() == OrderType.FillOrKill && !CanFullyMatch(order.GetSide(), order.GetPrice(), order.GetInitialQuantity()))
            {
                return Array.Empty<TradeNamespace.Trade>();
            }

            // Add the order to the appropriate side of the order book
            var level = order.GetSide() == Side.Buy
                ? _bids.GetOrCreateLevel(order.GetPrice())
                : _asks.GetOrCreateLevel(order.GetPrice());
            var orderNode = level.Add(order);

            // Add the order to the dictionary of orders
            _orders[order.GetOrderId()] = orderNode;

            return MatchOrders();
        }
    }

    public void CancelOrder(OrderId orderId)
    {
        lock (_mutex)
        {
            _orders.TryGetValue(orderId, out var orderNode);
            if (orderNode == null) return;

            var order = orderNode.Value;
            var ladder = order.GetSide() == Side.Buy ? _bids : _asks;
            if (ladder.TryGetLevel(order.GetPrice(), out var level))
            {
                level.Remove(orderNode);
                ladder.RemoveLevelIfEmpty(order.GetPrice(), level);
            }

            _orders.TryRemove(orderId, out _);
        }
    }

    public IReadOnlyList<TradeNamespace.Trade> ModifyOrder(ModifyOrderCommand modifyOrderCommand)
    {
        lock (_mutex)
        {
            if (!_orders.TryGetValue(modifyOrderCommand.GetOrderId(), out var orderNode))
            {
                return Array.Empty<TradeNamespace.Trade>();
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

            foreach (var (price, level) in _asks)
            {
                asks.Add(new Tick { price = price, quantity = level.TotalQuantity });
            }

            foreach (var (price, level) in _bids)
            {
                bids.Add(new Tick { price = price, quantity = level.TotalQuantity });
            }

            return new OrderBookTickInfos(asks, bids);
        }
    }

    private TimeSpan GetDelayUntilNextMidnight()
    {
        var now = _timeProvider.GetLocalNow().DateTime;
        var nextMidnight = now.Date.AddDays(1);
        return nextMidnight - now;
    }

    // Timer callback: fires at each upcoming midnight (per _timeProvider), prunes
    // any GoodForDay orders still resting in the book, then reschedules itself for
    // the following midnight.
    private void PruneGoodForDayOrdersAndScheduleNext()
    {
        PruneGoodForDayOrders();
        _pruneTimer.Change(GetDelayUntilNextMidnight(), Timeout.InfiniteTimeSpan);
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
        // DisposeAsync's returned task completes once any in-flight callback has
        // finished and no further callbacks will fire; blocking on it here keeps
        // Dispose's synchronous contract while preserving that guarantee.
        _pruneTimer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
