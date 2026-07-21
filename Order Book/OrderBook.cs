using OrderBook.Classes;
using OrderBook.Enums;
using OrderBook.Interfaces;
using OrderBook.OrderCommands;
using OrderBook.Structs;
using TradeNamespace = OrderBook.Trade;

namespace OrderBook;

public class OrderBook
{
    private readonly SortedDictionary<Price, LinkedList<Order>> _asks = new();
    private readonly SortedDictionary<Price, LinkedList<Order>> _bids =
        new SortedDictionary<Price, LinkedList<Order>>(Comparer<Price>.Create((x, y) => y.Value.CompareTo(x.Value)));
    private readonly Dictionary<OrderId, LinkedListNode<Order>> _orders = new();

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
                    _orders.Remove(bid.GetOrderId());
                }
                if (ask.GetRemainingQuantity().Value == 0)
                {
                    asks.RemoveFirst();
                    _orders.Remove(ask.GetOrderId());
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
            }

            // At the end check if the linked lists are empty and remove the price level from the tree if they are
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
            var (_, bids) = _bids.First();
            var order = bids.First?.Value;
            if (order != null && order.GetOrderType() == OrderType.FillAndKill)
            {
                CancelOrder(order.GetOrderId());
            }
        }

        if (_asks.Count > 0)
        {
            var (_, asks) = _asks.First();
            var order = asks.First?.Value;
            if (order != null && order.GetOrderType() == OrderType.FillAndKill)
            {
                CancelOrder(order.GetOrderId());
            }
        }

        return trades;
    }

    public List<TradeNamespace.Trade> AddOrder(Order order)
    {
        // if order already exists, throw an exception
        if (_orders.ContainsKey(order.GetOrderId()))
        {
            return new List<TradeNamespace.Trade>();
        }
        
        // if the order is a FillAndKill order and cannot be matched, throw an exception
        if (order.GetOrderType() == OrderType.FillAndKill && !CanMatch(order.GetSide(), order.GetPrice()))
        {
            return new List<TradeNamespace.Trade>();
        }

        // Add the order to the appropriate side of the order book 
        if (order.GetSide() == Side.Buy)
        {
            if (!_bids.TryGetValue(order.GetPrice(), out var bids))
            {
                bids = new LinkedList<Order>();
                _bids[order.GetPrice()] = bids;
            }

            bids.AddLast(order);
        }
        else
        {
            if (!_asks.TryGetValue(order.GetPrice(), out var asks))
            {
                asks = new LinkedList<Order>();
                _asks[order.GetPrice()] = asks;
            }

            asks.AddLast(order);
        }

        // Add the order to the dictionary of orders
        _orders[order.GetOrderId()] = new LinkedListNode<Order>(order);

        return MatchOrders();
    }

    public void CancelOrder(OrderId orderId)
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
        
        _orders.Remove(orderId);
    }

    public List<TradeNamespace.Trade> ModifyOrder(ModifyOrderCommand modifyOrderCommand)
    {
        if (!_orders.TryGetValue(modifyOrderCommand.GetOrderId(), out var orderNode))
        {
            return new List<TradeNamespace.Trade>();
        }

        var order = orderNode.Value;
        CancelOrder(order.GetOrderId());
        return AddOrder(modifyOrderCommand.ToOrder(order.GetOrderType()));
    }

    public int GetOrderCount()
    {
        return _orders.Count;
    }

    public IOrderBookTicksInfos GetOrderBookTickInfos()
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