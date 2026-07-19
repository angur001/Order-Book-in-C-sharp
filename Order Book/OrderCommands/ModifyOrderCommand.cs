using Order_Book.classes;
using Order_Book.enums;

namespace Order_Book.OrderCommands
{
    public class ModifyOrderCommand
    {
        private orderId _orderId;
        private Price _price;
        private Quantity _quantity;
        private Side _side;
        
        public ModifyOrderCommand(orderId orderId, Price newPrice, Quantity newQuantity, Side newSide)
        {
            _orderId = orderId;
            _price = newPrice;
            _quantity = newQuantity;
            _side = newSide;
        }

        public orderId GetOrderId() => _orderId;
        public Price GetPrice() => _price;
        public Quantity GetQuantity() => _quantity;
        public Side GetSide() => _side;
        public Order execute(OrderType orderType)
        {
            return new Order(_orderId, _price, _quantity, _side, orderType);
        }
    }
}