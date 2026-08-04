using OrderBook;

namespace OrderBook.Structs;

public readonly record struct TradeInfo(OrderId orderId, Price price, Quantity quantity);