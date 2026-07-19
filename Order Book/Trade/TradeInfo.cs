namespace Order_Book.structs
{
    public struct TradeInfo
    {
        public orderId orderId { get; set; }
        public Price price { get; set; }
        public Quantity quantity { get; set; }
    }
}