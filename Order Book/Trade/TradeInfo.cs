using OrderBook;

namespace OrderBook.Structs;

public struct TradeInfo
{
    public TradeInfo(OrderId orderId, Price price, Quantity quantity)
    {
        this.orderId = orderId;
        this.price = price;
        this.quantity = quantity;
    }

    public OrderId orderId { get; set; }
    public Price price { get; set; }
    public Quantity quantity { get; set; }
}