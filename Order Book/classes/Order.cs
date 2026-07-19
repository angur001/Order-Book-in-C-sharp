using Order_Book.enums;

namespace Order_Book.classes
{
    public class Order 
    {
        private Price _price { get; set; }
        private orderId _orderId { get; set; }
        private Side _side { get; set; }
        private OrderType _orderType { get; set; }
        private Quantity _initialQuantity { get; set; }
        private Quantity _remainingQuantity { get; set; }

        public Order(orderId orderId, Price price, Quantity quantity, Side side, OrderType orderType)
        {
            _price = price;
            _side = side;
            _orderType = orderType;
            _orderId = orderId;
            _initialQuantity = quantity;
            _remainingQuantity = quantity;
        }

        public orderId GetOrderId() => _orderId;
        public Price GetPrice() => _price;
        public Side GetSide() => _side;
        public OrderType GetOrderType() => _orderType;
        public Quantity GetInitialQuantity() => _initialQuantity;
        public Quantity GetRemainingQuantity() => _remainingQuantity;
        public Quantity GetExecutedQuantity() => _initialQuantity - _remainingQuantity;

        public void Fill(Quantity quantity)
        {
            if (quantity > _remainingQuantity)
                throw new ArgumentException($" (Order:{GetOrderId()}): Quantity to fill exceeds remaining quantity.");
            _remainingQuantity -= quantity;
        }

    }
}