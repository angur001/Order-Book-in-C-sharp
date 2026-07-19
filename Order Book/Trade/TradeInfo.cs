namespace Order_Book.structs
{
    public struct TradeInfo
    {
        public TradeInfo(orderId orderId, Price price, Quantity quantity)
        {
            this.orderId = orderId;
            this.price = price;
            this.quantity = quantity;
        }
        public orderId orderId { get; set; }
        public Price price { get; set; }
        public Quantity quantity { get; set; }
    }
}