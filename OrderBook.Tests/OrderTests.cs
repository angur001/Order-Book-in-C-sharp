using OrderBook;
using OrderBook.Classes;
using OrderBook.Enums;

namespace OrderBook.Tests;

public class OrderTests
{
    [Fact]
    public void Constructor_SetsAllFieldsFromArguments()
    {
        var order = new Order(new OrderId(1), new Price(100m), new Quantity(10), Side.Buy, OrderType.GoodTillCancel);

        Assert.Equal(new OrderId(1), order.GetOrderId());
        Assert.Equal(new Price(100m), order.GetPrice());
        Assert.Equal(Side.Buy, order.GetSide());
        Assert.Equal(OrderType.GoodTillCancel, order.GetOrderType());
        Assert.Equal(new Quantity(10), order.GetInitialQuantity());
        Assert.Equal(new Quantity(10), order.GetRemainingQuantity());
        Assert.False(order.IsFilled());
    }

    [Fact]
    public void Fill_ReducesRemainingQuantity_AndTracksExecutedQuantity()
    {
        var order = new Order(new OrderId(1), new Price(100m), new Quantity(10), Side.Buy, OrderType.GoodTillCancel);

        order.Fill(new Quantity(4));

        Assert.Equal(new Quantity(6), order.GetRemainingQuantity());
        Assert.Equal(new Quantity(4), order.GetExecutedQuantity());
        Assert.False(order.IsFilled());
    }

    [Fact]
    public void Fill_ExactRemainingQuantity_MarksOrderFilled()
    {
        var order = new Order(new OrderId(1), new Price(100m), new Quantity(10), Side.Buy, OrderType.GoodTillCancel);

        order.Fill(new Quantity(10));

        Assert.Equal(new Quantity(0), order.GetRemainingQuantity());
        Assert.True(order.IsFilled());
    }

    [Fact]
    public void Fill_MoreThanRemainingQuantity_Throws()
    {
        var order = new Order(new OrderId(1), new Price(100m), new Quantity(10), Side.Buy, OrderType.GoodTillCancel);
        order.Fill(new Quantity(7));

        Assert.Throws<ArgumentException>(() => order.Fill(new Quantity(4)));
    }

    [Fact]
    public void ToGoodTillCancel_ReturnsNewInstanceWithUpdatedPriceAndType_ButDoesNotMutateOriginal()
    {
        var original = new Order(new OrderId(1), Price.InvalidPrice, new Quantity(10), Side.Buy, OrderType.Market);

        var converted = original.ToGoodTillCancel(new Price(105m));

        Assert.Equal(new Price(105m), converted.GetPrice());
        Assert.Equal(OrderType.GoodTillCancel, converted.GetOrderType());

        // ToGoodTillCancel returns a *new* Order rather than mutating `original` -
        // callers (e.g. OrderBook.AddOrder) must use the returned instance.
        Assert.Equal(Price.InvalidPrice, original.GetPrice());
        Assert.Equal(OrderType.Market, original.GetOrderType());
    }

    [Fact]
    public void Constructor_ThreeArgOverload_BuildsAMarketOrderWithInvalidPrice()
    {
        var order = new Order(new OrderId(42), new Quantity(10), Side.Sell);

        Assert.Equal(new OrderId(42), order.GetOrderId());
        Assert.Equal(Price.InvalidPrice, order.GetPrice());
        Assert.Equal(Side.Sell, order.GetSide());
        Assert.Equal(OrderType.Market, order.GetOrderType());
        Assert.Equal(new Quantity(10), order.GetInitialQuantity());
    }
}
