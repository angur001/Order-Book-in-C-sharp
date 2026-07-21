using OrderBook;
using OrderBook.Classes;
using OrderBook.Enums;

namespace OrderBook.OrderCommands;

public class ModifyOrderCommand
{
    private OrderId _orderId;
    private Price _price;
    private Quantity _quantity;
    private Side _side;

    public ModifyOrderCommand(OrderId orderId, Price newPrice, Quantity newQuantity, Side newSide)
    {
        _orderId = orderId;
        _price = newPrice;
        _quantity = newQuantity;
        _side = newSide;
    }

    public OrderId GetOrderId() => _orderId;
    public Price GetPrice() => _price;
    public Quantity GetQuantity() => _quantity;
    public Side GetSide() => _side;
    public Order execute(OrderType orderType)
    {
        return new Order(_orderId, _price, _quantity, _side, orderType);
    }
}