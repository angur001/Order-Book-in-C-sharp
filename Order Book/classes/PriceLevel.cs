namespace OrderBook.Classes;

// All the resting orders at a single price, plus their aggregate remaining
// quantity kept in lock-step with the order list itself. Folding the running
// total into the same object as the orders (rather than a separate
// price-keyed dictionary) means there is exactly one place that can drift out
// of sync with the order list - and one lookup instead of two on every
// add/cancel/match.
public sealed class PriceLevel
{
    private readonly LinkedList<Order> _orders = new();

    public LinkedList<Order> Orders => _orders;
    public int Count => _orders.Count;
    public Quantity TotalQuantity { get; private set; }

    public LinkedListNode<Order> Add(Order order)
    {
        TotalQuantity += order.GetRemainingQuantity();
        return _orders.AddLast(order);
    }

    // Called when a resting order is cancelled outright (as opposed to being
    // consumed by a trade): removes whatever quantity it still had resting.
    public void Remove(LinkedListNode<Order> node)
    {
        TotalQuantity -= node.Value.GetRemainingQuantity();
        _orders.Remove(node);
    }

    // Called once per fill, whether it fully or partially fills the order.
    // The LinkedList removal for a fully-filled order (RemoveFirst) is a
    // separate step handled by the caller in MatchOrders, same as before.
    public void RecordFill(Quantity tradeQuantity)
    {
        TotalQuantity -= tradeQuantity;
    }

    // Restores a pooled instance to the state a freshly-`new`ed PriceLevel
    // would be in, so ladders can hand it back out via GetOrCreateLevel.
    internal void Reset()
    {
        _orders.Clear();
        TotalQuantity = default;
    }
}
