using Order_Book.enums;

namespace Order_Book.classes
{
    public class Order 
    {
        private decimal _price { get; set; }
        private uint _quantity { get; set; }
        private uint _orderId { get; set; }
        private Side _side { get; set; }
        private OrderType _orderType { get; set; }
        private uint _initialQuantity { get; set; }
        private uint _remainingQuantity { get; set; }

        public Order(uint orderId, decimal price, uint quantity, Side side, OrderType orderType)
        {
            _price = price;
            _quantity = quantity;
            _side = side;
            _orderType = orderType;
            _orderId = orderId;
            _initialQuantity = quantity;
            _remainingQuantity = quantity;
        }
    }
}