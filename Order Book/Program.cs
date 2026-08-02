using OrderBook;
using OrderBook.Classes;
using OrderBook.Enums;

public class Program
{
    static void Main(string[] args)
    {
        // test order book functionality here
        var orderBook = new OrderBook.OrderBook();
        Console.WriteLine($"Order count: {orderBook.GetOrderCount()}");
        var order1 = new Order(new OrderId(1), new Price(100.0m), new Quantity(10), Side.Buy, OrderType.GoodTillCancel);
        var order2 = new Order(new OrderId(2), new Price(101.0m), new Quantity(5), Side.Sell, OrderType.GoodTillCancel);
        orderBook.AddOrder(order1);
        orderBook.AddOrder(order2);
        Console.WriteLine($"Order count: {orderBook.GetOrderCount()}");
    }
}
