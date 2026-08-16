using OrderBook;
using OrderBook.Classes;
using OrderBook.Enums;
using OrderBook.Interfaces;

namespace OrderBook.Tests;

// The IPriceLadder contract, written entirely against the interface rather
// than any concrete implementation's own members. Each strategy gets this
// whole suite for free by subclassing and providing its own two ladders
// (see TreePriceLadderTests, ArrayPriceLadderTests) - which is exactly what
// proves a new implementation is behaviorally interchangeable with the
// others, not just independently self-consistent.
public abstract class PriceLadderContractTests
{
    protected abstract IPriceLadder CreateAscendingLadder();
    protected abstract IPriceLadder CreateDescendingLadder();

    [Fact]
    public void NewLadder_IsEmpty()
    {
        var ladder = CreateAscendingLadder();

        Assert.Equal(0, ladder.Count);
        Assert.Null(ladder.WorstPrice);
        Assert.False(ladder.TryGetLevel(new Price(100m), out _));
    }

    [Fact]
    public void GetOrCreateLevel_FirstCall_CreatesANewEmptyLevel()
    {
        var ladder = CreateAscendingLadder();

        var level = ladder.GetOrCreateLevel(new Price(100m));

        Assert.Equal(1, ladder.Count);
        Assert.Equal(0, level.Count);
        Assert.Equal(new Quantity(0), level.TotalQuantity);
    }

    [Fact]
    public void GetOrCreateLevel_SecondCallAtSamePrice_ReturnsTheSameInstance()
    {
        var ladder = CreateAscendingLadder();

        var first = ladder.GetOrCreateLevel(new Price(100m));
        var second = ladder.GetOrCreateLevel(new Price(100m));

        Assert.Same(first, second);
        Assert.Equal(1, ladder.Count);
    }

    [Fact]
    public void First_ReturnsTheBestPriceAccordingToTheComparer()
    {
        var ascending = CreateAscendingLadder();
        ascending.GetOrCreateLevel(new Price(105m));
        ascending.GetOrCreateLevel(new Price(100m));
        ascending.GetOrCreateLevel(new Price(110m));
        Assert.Equal(new Price(100m), ascending.First().Key);

        var descending = CreateDescendingLadder();
        descending.GetOrCreateLevel(new Price(105m));
        descending.GetOrCreateLevel(new Price(100m));
        descending.GetOrCreateLevel(new Price(110m));
        Assert.Equal(new Price(110m), descending.First().Key);
    }

    [Fact]
    public void Enumeration_VisitsLevelsInBestToWorstOrder()
    {
        var ladder = CreateAscendingLadder();
        ladder.GetOrCreateLevel(new Price(105m));
        ladder.GetOrCreateLevel(new Price(100m));
        ladder.GetOrCreateLevel(new Price(110m));

        var prices = ladder.Select(kvp => kvp.Key).ToList();

        Assert.Equal([new Price(100m), new Price(105m), new Price(110m)], prices);
    }

    [Fact]
    public void WorstPrice_TracksTheWorstLevelAsLevelsAreCreated()
    {
        var ladder = CreateAscendingLadder();
        ladder.GetOrCreateLevel(new Price(100m));
        Assert.Equal(new Price(100m), ladder.WorstPrice);

        ladder.GetOrCreateLevel(new Price(105m)); // worse (higher) than current worst
        Assert.Equal(new Price(105m), ladder.WorstPrice);

        ladder.GetOrCreateLevel(new Price(102m)); // better than current worst - shouldn't change it
        Assert.Equal(new Price(105m), ladder.WorstPrice);
    }

    [Fact]
    public void RemoveLevelIfEmpty_WithOrdersStillResting_IsANoOp()
    {
        var ladder = CreateAscendingLadder();
        var level = ladder.GetOrCreateLevel(new Price(100m));
        level.Add(new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Buy, OrderType.GoodTillCancel));

        ladder.RemoveLevelIfEmpty(new Price(100m), level);

        Assert.Equal(1, ladder.Count);
        Assert.True(ladder.TryGetLevel(new Price(100m), out _));
    }

    [Fact]
    public void RemoveLevelIfEmpty_LastLevel_ClearsWorstPriceToNull()
    {
        var ladder = CreateAscendingLadder();
        var level = ladder.GetOrCreateLevel(new Price(100m));

        ladder.RemoveLevelIfEmpty(new Price(100m), level);

        Assert.Equal(0, ladder.Count);
        Assert.Null(ladder.WorstPrice);
    }

    // Implementations may pool and reuse the PriceLevel instance backing a
    // slot/key once it empties out (see TreePriceLadder/ArrayPriceLadder), so
    // this pins the observable contract: whatever comes back from
    // GetOrCreateLevel after a level has emptied and been removed must look
    // exactly like a brand new level, regardless of instance identity.
    [Fact]
    public void GetOrCreateLevel_AfterLevelEmptiedAndRemoved_BehavesLikeAFreshLevel()
    {
        var ladder = CreateAscendingLadder();
        var price = new Price(100m);

        var level = ladder.GetOrCreateLevel(price);
        var node = level.Add(new Order(new OrderId(1), price, new Quantity(5), Side.Buy, OrderType.GoodTillCancel));
        level.Remove(node);
        ladder.RemoveLevelIfEmpty(price, level);
        Assert.Equal(0, ladder.Count);

        var recreatedLevel = ladder.GetOrCreateLevel(price);

        Assert.Equal(1, ladder.Count);
        Assert.Equal(0, recreatedLevel.Count);
        Assert.Equal(new Quantity(0), recreatedLevel.TotalQuantity);
        Assert.Empty(recreatedLevel.Orders);
    }

    // The riskiest part of any caching implementation: WorstPrice (and, for an
    // array-backed ladder, BestPrice too) isn't a live computation, so removing
    // the level that happens to be the cached one must recompute it from
    // what's actually left - not leave it pointing at a price that no longer
    // has a level at all.
    [Fact]
    public void RemoveLevelIfEmpty_RemovingTheCachedWorstLevel_RecomputesWorstPriceFromWhatRemains()
    {
        var ladder = CreateAscendingLadder();
        ladder.GetOrCreateLevel(new Price(100m));
        var worstLevel = ladder.GetOrCreateLevel(new Price(105m));
        Assert.Equal(new Price(105m), ladder.WorstPrice); // sanity check before the removal under test

        ladder.RemoveLevelIfEmpty(new Price(105m), worstLevel);

        Assert.Equal(new Price(100m), ladder.WorstPrice);
        Assert.Equal(1, ladder.Count);
    }

    [Fact]
    public void RemoveLevelIfEmpty_RemovingTheCachedBestLevel_RecomputesFirstFromWhatRemains()
    {
        var ladder = CreateAscendingLadder();
        var bestLevel = ladder.GetOrCreateLevel(new Price(100m));
        ladder.GetOrCreateLevel(new Price(105m));
        Assert.Equal(new Price(100m), ladder.First().Key); // sanity check before the removal under test

        ladder.RemoveLevelIfEmpty(new Price(100m), bestLevel);

        Assert.Equal(new Price(105m), ladder.First().Key);
        Assert.Equal(1, ladder.Count);
    }

    [Fact]
    public void RemoveLevelIfEmpty_RemovingANonWorstLevel_LeavesWorstPriceUnchanged()
    {
        var ladder = CreateAscendingLadder();
        var bestLevel = ladder.GetOrCreateLevel(new Price(100m));
        ladder.GetOrCreateLevel(new Price(105m));

        ladder.RemoveLevelIfEmpty(new Price(100m), bestLevel);

        Assert.Equal(new Price(105m), ladder.WorstPrice);
        Assert.Equal(1, ladder.Count);
    }

    [Fact]
    public void RemoveLevelIfEmpty_MiddleLevel_LeavesBestAndWorstUnchanged()
    {
        var ladder = CreateAscendingLadder();
        ladder.GetOrCreateLevel(new Price(100m));
        var middleLevel = ladder.GetOrCreateLevel(new Price(105m));
        ladder.GetOrCreateLevel(new Price(110m));

        ladder.RemoveLevelIfEmpty(new Price(105m), middleLevel);

        Assert.Equal(new Price(100m), ladder.First().Key);
        Assert.Equal(new Price(110m), ladder.WorstPrice);
        Assert.Equal(2, ladder.Count);
    }
}
