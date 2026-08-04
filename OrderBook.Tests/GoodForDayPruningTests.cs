using Microsoft.Extensions.Time.Testing;
using OrderBook.Classes;
using OrderBook.Enums;
using OB = OrderBook.OrderBook;

namespace OrderBook.Tests;

public class GoodForDayPruningTests
{
    private static FakeTimeProvider CreateTimeProviderAt(int hourUtc)
    {
        // FakeTimeProvider's LocalTimeZone defaults to UTC, matching the hours used below.
        return new FakeTimeProvider(new DateTimeOffset(2026, 8, 4, hourUtc, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void FiresAtMidnight_CancelsRestingGoodForDayOrders()
    {
        var timeProvider = CreateTimeProviderAt(10);
        using var book = new OB(timeProvider);
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Buy, OrderType.GoodForDay));

        timeProvider.Advance(TimeSpan.FromHours(14) + TimeSpan.FromSeconds(1)); // just past the next midnight

        Assert.Equal(0, book.GetOrderCount());
    }

    [Fact]
    public void DoesNotFireBeforeMidnight()
    {
        var timeProvider = CreateTimeProviderAt(10);
        using var book = new OB(timeProvider);
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Buy, OrderType.GoodForDay));

        timeProvider.Advance(TimeSpan.FromHours(13)); // still before midnight

        Assert.Equal(1, book.GetOrderCount());
    }

    [Fact]
    public void OnlyCancelsGoodForDayOrders_LeavesOtherOrderTypesResting()
    {
        var timeProvider = CreateTimeProviderAt(10);
        using var book = new OB(timeProvider);
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Buy, OrderType.GoodForDay));
        book.AddOrder(new Order(new OrderId(2), new Price(90m), new Quantity(3), Side.Buy, OrderType.GoodTillCancel));

        timeProvider.Advance(TimeSpan.FromHours(14) + TimeSpan.FromSeconds(1));

        Assert.Equal(1, book.GetOrderCount());
        var bids = book.GetOrderBookTickInfos().GetBids().ToList();
        Assert.Single(bids);
        Assert.Equal(new Price(90m), bids[0].price);
    }

    [Fact]
    public void ReschedulesItselfForTheFollowingMidnight()
    {
        var timeProvider = CreateTimeProviderAt(10);
        using var book = new OB(timeProvider);
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Buy, OrderType.GoodForDay));

        timeProvider.Advance(TimeSpan.FromHours(14) + TimeSpan.FromSeconds(1)); // past the 1st midnight
        Assert.Equal(0, book.GetOrderCount());

        book.AddOrder(new Order(new OrderId(2), new Price(100m), new Quantity(5), Side.Buy, OrderType.GoodForDay));
        timeProvider.Advance(TimeSpan.FromHours(24)); // past the 2nd midnight too

        Assert.Equal(0, book.GetOrderCount());
    }
}
