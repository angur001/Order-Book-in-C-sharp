using OrderBook;
using OrderBook.Classes;
using OrderBook.Enums;
using OrderBook.Interfaces;
using OrderBook.OrderCommands;
using OB = OrderBook.OrderBook;

namespace OrderBook.Tests;

public class OrderBookTests
{
    // ---------------------------------------------------------------
    // AddOrder: happy path
    // ---------------------------------------------------------------

    [Fact]
    public void AddOrder_NonCrossingOrders_ProduceNoTradesAndBothRest()
    {
        using var book = new OB();
        var bid = new Order(new OrderId(1), new Price(100m), new Quantity(10), Side.Buy, OrderType.GoodTillCancel);
        var ask = new Order(new OrderId(2), new Price(105m), new Quantity(5), Side.Sell, OrderType.GoodTillCancel);

        var bidTrades = book.AddOrder(bid);
        var askTrades = book.AddOrder(ask);

        Assert.Empty(bidTrades);
        Assert.Empty(askTrades);
        Assert.Equal(2, book.GetOrderCount());
    }

    [Fact]
    public void AddOrder_CrossingOrders_SameQuantity_FullyMatchesBoth()
    {
        using var book = new OB();
        var ask = new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Sell, OrderType.GoodTillCancel);
        var bid = new Order(new OrderId(2), new Price(100m), new Quantity(5), Side.Buy, OrderType.GoodTillCancel);
        book.AddOrder(ask);

        var trades = book.AddOrder(bid);

        Assert.Single(trades);
        var trade = trades[0];
        Assert.Equal(new OrderId(2), trade.GetBidTrade().orderId);
        Assert.Equal(new Quantity(5), trade.GetBidTrade().quantity);
        Assert.Equal(new OrderId(1), trade.GetAskTrade().orderId);
        Assert.Equal(new Quantity(5), trade.GetAskTrade().quantity);
        Assert.Equal(0, book.GetOrderCount());
    }

    [Fact]
    public void AddOrder_PartialFill_LeavesRemainderResting()
    {
        using var book = new OB();
        var bid = new Order(new OrderId(1), new Price(100m), new Quantity(10), Side.Buy, OrderType.GoodTillCancel);
        book.AddOrder(bid);
        var ask = new Order(new OrderId(2), new Price(100m), new Quantity(4), Side.Sell, OrderType.GoodTillCancel);

        var trades = book.AddOrder(ask);

        Assert.Single(trades);
        Assert.Equal(new Quantity(4), trades[0].GetBidTrade().quantity);
        Assert.Equal(1, book.GetOrderCount());

        var bids = book.GetOrderBookTickInfos().GetBids().ToList();
        Assert.Single(bids);
        Assert.Equal(new Quantity(6), bids[0].quantity);
    }

    [Fact]
    public void AddOrder_SamePriceLevel_MatchesInFifoOrder()
    {
        using var book = new OB();
        var firstAsk = new Order(new OrderId(1), new Price(100m), new Quantity(3), Side.Sell, OrderType.GoodTillCancel);
        var secondAsk = new Order(new OrderId(2), new Price(100m), new Quantity(7), Side.Sell, OrderType.GoodTillCancel);
        book.AddOrder(firstAsk);
        book.AddOrder(secondAsk);
        var bid = new Order(new OrderId(3), new Price(100m), new Quantity(3), Side.Buy, OrderType.GoodTillCancel);

        var trades = book.AddOrder(bid);

        Assert.Single(trades);
        Assert.Equal(new OrderId(1), trades[0].GetAskTrade().orderId);
        Assert.Equal(new Quantity(3), trades[0].GetAskTrade().quantity);
        // The first order in at a price level must be matched before later ones (FIFO).
        Assert.Equal(1, book.GetOrderCount());
        var asks = book.GetOrderBookTickInfos().GetAsks().ToList();
        Assert.Equal(new Quantity(7), asks[0].quantity);
    }

    [Fact]
    public void AddOrder_MatchesBestPriceLevelFirst_RegardlessOfInsertionOrder()
    {
        using var book = new OB();
        var worseAsk = new Order(new OrderId(1), new Price(102m), new Quantity(5), Side.Sell, OrderType.GoodTillCancel);
        var betterAsk = new Order(new OrderId(2), new Price(100m), new Quantity(5), Side.Sell, OrderType.GoodTillCancel);
        book.AddOrder(worseAsk); // inserted first, but is the worse price for a buyer
        book.AddOrder(betterAsk);
        var bid = new Order(new OrderId(3), new Price(102m), new Quantity(5), Side.Buy, OrderType.GoodTillCancel);

        var trades = book.AddOrder(bid);

        Assert.Single(trades);
        Assert.Equal(new OrderId(2), trades[0].GetAskTrade().orderId); // the cheaper (better) ask, not the first-inserted one
        Assert.Equal(new Price(102m), trades[0].GetBidTrade().price); // each side records its own order price
        Assert.Equal(new Price(100m), trades[0].GetAskTrade().price);
        Assert.Equal(1, book.GetOrderCount()); // worseAsk still resting untouched
    }

    [Fact]
    public void AddOrder_SweepsMultipleAskLevels_ForALargeEnoughBuyOrder()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Sell, OrderType.GoodTillCancel));
        book.AddOrder(new Order(new OrderId(2), new Price(101m), new Quantity(5), Side.Sell, OrderType.GoodTillCancel));
        var bid = new Order(new OrderId(3), new Price(101m), new Quantity(8), Side.Buy, OrderType.GoodTillCancel);

        var trades = book.AddOrder(bid);

        Assert.Equal(2, trades.Count);
        Assert.Equal(new OrderId(1), trades[0].GetAskTrade().orderId);
        Assert.Equal(new Quantity(5), trades[0].GetAskTrade().quantity);
        Assert.Equal(new OrderId(2), trades[1].GetAskTrade().orderId);
        Assert.Equal(new Quantity(3), trades[1].GetAskTrade().quantity);

        // id1 fully filled, id2 has 2 left resting, id3 (the buy) fully filled.
        Assert.Equal(1, book.GetOrderCount());
        var asks = book.GetOrderBookTickInfos().GetAsks().ToList();
        Assert.Single(asks);
        Assert.Equal(new Quantity(2), asks[0].quantity);
    }

    // ---------------------------------------------------------------
    // AddOrder: degenerate / duplicate input
    // ---------------------------------------------------------------

    [Fact]
    public void AddOrder_DuplicateOrderId_IsIgnoredAndReturnsNoTrades()
    {
        using var book = new OB();
        var original = new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Buy, OrderType.GoodTillCancel);
        book.AddOrder(original);
        var duplicateId = new Order(new OrderId(1), new Price(200m), new Quantity(1), Side.Sell, OrderType.GoodTillCancel);

        var trades = book.AddOrder(duplicateId);

        Assert.Empty(trades);
        Assert.Equal(1, book.GetOrderCount());
        Assert.Empty(book.GetOrderBookTickInfos().GetAsks()); // the duplicate-id sell was never actually inserted
    }

    // ---------------------------------------------------------------
    // CancelOrder
    // ---------------------------------------------------------------

    [Fact]
    public void CancelOrder_ExistingOrder_RemovesItAndFreesThePriceLevel()
    {
        using var book = new OB();
        var order = new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Buy, OrderType.GoodTillCancel);
        book.AddOrder(order);

        book.CancelOrder(new OrderId(1));

        Assert.Equal(0, book.GetOrderCount());
        Assert.Empty(book.GetOrderBookTickInfos().GetBids());
    }

    [Fact]
    public void CancelOrder_NonExistentOrder_DoesNotThrowAndHasNoEffect()
    {
        using var book = new OB();

        book.CancelOrder(new OrderId(999));

        Assert.Equal(0, book.GetOrderCount());
    }

    [Fact]
    public void CancelOrder_OneOfSeveralAtSameLevel_UpdatesRemainingLevelQuantity()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Buy, OrderType.GoodTillCancel));
        book.AddOrder(new Order(new OrderId(2), new Price(100m), new Quantity(3), Side.Buy, OrderType.GoodTillCancel));

        book.CancelOrder(new OrderId(1));

        Assert.Equal(1, book.GetOrderCount());
        var bids = book.GetOrderBookTickInfos().GetBids().ToList();
        Assert.Single(bids);
        Assert.Equal(new Quantity(3), bids[0].quantity);
    }

    // ---------------------------------------------------------------
    // ModifyOrder
    // ---------------------------------------------------------------

    [Fact]
    public void ModifyOrder_NonExistentOrder_ReturnsEmptyAndDoesNotThrow()
    {
        using var book = new OB();
        var command = new ModifyOrderCommand(new OrderId(1), new Price(100m), new Quantity(5), Side.Buy);

        var trades = book.ModifyOrder(command);

        Assert.Empty(trades);
        Assert.Equal(0, book.GetOrderCount());
    }

    [Fact]
    public void ModifyOrder_ChangesPriceAndQuantity_WithoutCausingAMatch()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(98m), new Quantity(5), Side.Buy, OrderType.GoodTillCancel));
        var command = new ModifyOrderCommand(new OrderId(1), new Price(96m), new Quantity(8), Side.Buy);

        var trades = book.ModifyOrder(command);

        Assert.Empty(trades);
        Assert.Equal(1, book.GetOrderCount());
        var bids = book.GetOrderBookTickInfos().GetBids().ToList();
        Assert.Single(bids);
        Assert.Equal(new Price(96m), bids[0].price);
        Assert.Equal(new Quantity(8), bids[0].quantity);
    }

    [Fact]
    public void ModifyOrder_RepricingIntoTheBook_CanTriggerAnImmediateMatch()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(98m), new Quantity(5), Side.Buy, OrderType.GoodTillCancel));
        book.AddOrder(new Order(new OrderId(2), new Price(100m), new Quantity(4), Side.Sell, OrderType.GoodTillCancel));
        var command = new ModifyOrderCommand(new OrderId(1), new Price(101m), new Quantity(5), Side.Buy);

        var trades = book.ModifyOrder(command);

        Assert.Single(trades);
        Assert.Equal(new Quantity(4), trades[0].GetAskTrade().quantity);
        Assert.Equal(1, book.GetOrderCount()); // leftover qty 1 of the modified bid rests at the new price
        var bids = book.GetOrderBookTickInfos().GetBids().ToList();
        Assert.Single(bids);
        Assert.Equal(new Price(101m), bids[0].price);
        Assert.Equal(new Quantity(1), bids[0].quantity);
    }

    // ---------------------------------------------------------------
    // FillAndKill (IOC)
    // ---------------------------------------------------------------

    [Fact]
    public void AddOrder_FillAndKill_WithNoMatchingLiquidity_IsNeverAdded()
    {
        using var book = new OB();
        var order = new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Buy, OrderType.FillAndKill);

        var trades = book.AddOrder(order);

        Assert.Empty(trades);
        Assert.Equal(0, book.GetOrderCount());
    }

    [Fact]
    public void AddOrder_FillAndKill_PartialMatch_CancelsTheUnfilledRemainderInsteadOfResting()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(3), Side.Sell, OrderType.GoodTillCancel));
        var fillAndKill = new Order(new OrderId(2), new Price(100m), new Quantity(10), Side.Buy, OrderType.FillAndKill);

        var trades = book.AddOrder(fillAndKill);

        Assert.Single(trades);
        Assert.Equal(new Quantity(3), trades[0].GetBidTrade().quantity);
        // The 7 unfilled units of the FillAndKill order must NOT rest in the book.
        Assert.Equal(0, book.GetOrderCount());
    }

    [Fact]
    public void AddOrder_FillAndKill_FullMatch_BehavesLikeAnOrdinaryMatch()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Sell, OrderType.GoodTillCancel));
        var fillAndKill = new Order(new OrderId(2), new Price(100m), new Quantity(5), Side.Buy, OrderType.FillAndKill);

        var trades = book.AddOrder(fillAndKill);

        Assert.Single(trades);
        Assert.Equal(new Quantity(5), trades[0].GetBidTrade().quantity);
        Assert.Equal(0, book.GetOrderCount());
    }

    // ---------------------------------------------------------------
    // FillOrKill (all-or-nothing)
    // ---------------------------------------------------------------

    [Fact]
    public void AddOrder_FillOrKill_WhenItCannotBeFullyFilled_IsRejectedEntirely()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Sell, OrderType.GoodTillCancel));
        var fillOrKill = new Order(new OrderId(2), new Price(100m), new Quantity(10), Side.Buy, OrderType.FillOrKill);

        var trades = book.AddOrder(fillOrKill);

        Assert.Empty(trades);
        Assert.Equal(1, book.GetOrderCount()); // only the original resting ask
        var asks = book.GetOrderBookTickInfos().GetAsks().ToList();
        Assert.Single(asks);
        Assert.Equal(new Quantity(5), asks[0].quantity); // untouched, no partial fill leaked through
    }

    [Fact]
    public void AddOrder_FillOrKill_WhenItCanBeFullyFilledAcrossMultipleLevels_ExecutesCompletely()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Sell, OrderType.GoodTillCancel));
        book.AddOrder(new Order(new OrderId(2), new Price(101m), new Quantity(5), Side.Sell, OrderType.GoodTillCancel));
        var fillOrKill = new Order(new OrderId(3), new Price(101m), new Quantity(8), Side.Buy, OrderType.FillOrKill);

        var trades = book.AddOrder(fillOrKill);

        Assert.Equal(2, trades.Count);
        Assert.Equal(new Quantity(5), trades[0].GetAskTrade().quantity);
        Assert.Equal(new Quantity(3), trades[1].GetAskTrade().quantity);
        Assert.Equal(1, book.GetOrderCount()); // id2 has 2 units left resting
    }

    // ---------------------------------------------------------------
    // Market orders
    // ---------------------------------------------------------------

    [Fact]
    public void AddOrder_MarketBuyOrder_MatchesAtOppositeSidePrice_RegardlessOfItsOwnLimitPrice()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(101m), new Quantity(5), Side.Sell, OrderType.GoodTillCancel));
        // The limit price (1) is nonsense for a resting order but irrelevant for a market order:
        // it should reprice to the best available ask (101) and execute anyway.
        var marketBuy = new Order(new OrderId(2), new Price(1m), new Quantity(5), Side.Buy, OrderType.Market);

        var trades = book.AddOrder(marketBuy);

        Assert.Single(trades);
        Assert.Equal(new Quantity(5), trades[0].GetAskTrade().quantity);
        Assert.Equal(0, book.GetOrderCount());
    }

    [Fact]
    public void AddOrder_MarketOrder_BuiltViaConvenienceConstructor_AlsoSweepsTheBook()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(101m), new Quantity(5), Side.Sell, OrderType.GoodTillCancel));
        var marketBuy = new Order(new OrderId(2), new Quantity(5), Side.Buy); // 3-arg convenience ctor

        var trades = book.AddOrder(marketBuy);

        Assert.Single(trades);
        Assert.Equal(0, book.GetOrderCount());
    }

    [Fact]
    public void AddOrder_MarketSellOrder_SweepsMultipleBidLevels()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Buy, OrderType.GoodTillCancel));
        book.AddOrder(new Order(new OrderId(2), new Price(99m), new Quantity(5), Side.Buy, OrderType.GoodTillCancel));
        var marketSell = new Order(new OrderId(3), new Price(1000m), new Quantity(8), Side.Sell, OrderType.Market);

        var trades = book.AddOrder(marketSell);

        Assert.Equal(2, trades.Count);
        Assert.Equal(new Quantity(5), trades[0].GetBidTrade().quantity); // best bid (100) first
        Assert.Equal(new Quantity(3), trades[1].GetBidTrade().quantity); // then the worse bid (99)
        Assert.Equal(1, book.GetOrderCount()); // id2 has 2 units left resting at 99
    }

    // The worst-price used for Market repricing is a cached field maintained
    // incrementally on level create/remove (see GetOrCreate*Level/Remove*LevelIfEmpty
    // in OrderBook.cs) rather than recomputed from scratch each time. These two
    // tests specifically target the recompute-on-removal path: if the cache were
    // left stale after the level that used to be worst disappears, a leftover,
    // unfilled remainder would rest at the wrong (already-gone) price instead of
    // the new, actual worst price - which these tests would catch via the
    // resting price of that leftover.
    [Fact]
    public void AddOrder_MarketBuyOrder_AfterWorstAskLevelIsCancelled_RepricesLeftoverToTheNewWorstAsk()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Sell, OrderType.GoodTillCancel));
        book.AddOrder(new Order(new OrderId(2), new Price(105m), new Quantity(2), Side.Sell, OrderType.GoodTillCancel));
        book.CancelOrder(new OrderId(2)); // the 105 level - previously the worst ask - is now gone entirely

        var marketBuy = new Order(new OrderId(3), new Price(1m), new Quantity(10), Side.Buy, OrderType.Market);
        var trades = book.AddOrder(marketBuy);

        Assert.Single(trades);
        Assert.Equal(new Quantity(5), trades[0].GetAskTrade().quantity);
        var bids = book.GetOrderBookTickInfos().GetBids().ToList();
        Assert.Single(bids);
        Assert.Equal(new Price(100m), bids[0].price); // must reprice to the current worst (100), not the stale/gone 105
        Assert.Equal(new Quantity(5), bids[0].quantity); // 10 requested - 5 filled = 5 left resting
    }

    [Fact]
    public void AddOrder_MarketSellOrder_AfterWorstBidLevelIsCancelled_RepricesLeftoverToTheNewWorstBid()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Buy, OrderType.GoodTillCancel));
        book.AddOrder(new Order(new OrderId(2), new Price(95m), new Quantity(2), Side.Buy, OrderType.GoodTillCancel));
        book.CancelOrder(new OrderId(2)); // the 95 level - previously the worst bid - is now gone entirely

        var marketSell = new Order(new OrderId(3), new Price(1000m), new Quantity(10), Side.Sell, OrderType.Market);
        var trades = book.AddOrder(marketSell);

        Assert.Single(trades);
        Assert.Equal(new Quantity(5), trades[0].GetBidTrade().quantity);
        var asks = book.GetOrderBookTickInfos().GetAsks().ToList();
        Assert.Single(asks);
        Assert.Equal(new Price(100m), asks[0].price); // must reprice to the current worst (100), not the stale/gone 95
        Assert.Equal(new Quantity(5), asks[0].quantity); // 10 requested - 5 filled = 5 left resting
    }

    // With no opposite-side liquidity at all, AddOrder has no price to reprice
    // against, so the market order is inserted as-is (still OrderType.Market,
    // at whatever price it was constructed with) instead of matching or being
    // rejected. This pins down that corner case rather than asserting it's
    // "correct" either way.
    [Fact]
    public void AddOrder_MarketBuyOrder_WithNoOppositeLiquidity_RestsUnrepriced()
    {
        using var book = new OB();
        var marketBuy = new Order(new OrderId(1), new Quantity(5), Side.Buy);

        var trades = book.AddOrder(marketBuy);

        Assert.Empty(trades);
        Assert.Equal(1, book.GetOrderCount());
    }

    // ---------------------------------------------------------------
    // GoodForDay - bookkeeping only. See GoodForDayPruningTests for coverage
    // of the actual midnight-triggered pruning, using an injected FakeTimeProvider.
    // ---------------------------------------------------------------

    [Fact]
    public void AddOrder_GoodForDayOrder_RestsNormallyLikeAnyOtherOrder()
    {
        using var book = new OB();
        var order = new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Buy, OrderType.GoodForDay);

        var trades = book.AddOrder(order);

        Assert.Empty(trades);
        Assert.Equal(1, book.GetOrderCount());
    }

    // ---------------------------------------------------------------
    // GetOrderBookTickInfos
    // ---------------------------------------------------------------

    [Fact]
    public void GetOrderBookTickInfos_AggregatesQuantityAcrossOrdersAtTheSameLevel()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Buy, OrderType.GoodTillCancel));
        book.AddOrder(new Order(new OrderId(2), new Price(100m), new Quantity(3), Side.Buy, OrderType.GoodTillCancel));

        var bids = book.GetOrderBookTickInfos().GetBids().ToList();

        Assert.Single(bids);
        Assert.Equal(new Price(100m), bids[0].price);
        Assert.Equal(new Quantity(8), bids[0].quantity);
    }

    [Fact]
    public void GetOrderBookTickInfos_OrdersAsksAscending_AndBidsDescending()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(105m), new Quantity(1), Side.Sell, OrderType.GoodTillCancel));
        book.AddOrder(new Order(new OrderId(2), new Price(103m), new Quantity(1), Side.Sell, OrderType.GoodTillCancel));
        book.AddOrder(new Order(new OrderId(3), new Price(107m), new Quantity(1), Side.Sell, OrderType.GoodTillCancel));
        book.AddOrder(new Order(new OrderId(4), new Price(95m), new Quantity(1), Side.Buy, OrderType.GoodTillCancel));
        book.AddOrder(new Order(new OrderId(5), new Price(99m), new Quantity(1), Side.Buy, OrderType.GoodTillCancel));
        book.AddOrder(new Order(new OrderId(6), new Price(90m), new Quantity(1), Side.Buy, OrderType.GoodTillCancel));

        var asks = book.GetOrderBookTickInfos().GetAsks().Select(t => t.price.Value).ToList();
        var bids = book.GetOrderBookTickInfos().GetBids().Select(t => t.price.Value).ToList();

        Assert.Equal(new List<decimal> { 103m, 105m, 107m }, asks); // best (lowest) ask first
        Assert.Equal(new List<decimal> { 99m, 95m, 90m }, bids); // best (highest) bid first
    }

    [Fact]
    public void GetOrderBookTickInfos_GetTicks_ReturnsBothSidesCombined()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(105m), new Quantity(2), Side.Sell, OrderType.GoodTillCancel));
        book.AddOrder(new Order(new OrderId(2), new Price(95m), new Quantity(3), Side.Buy, OrderType.GoodTillCancel));

        var ticks = book.GetOrderBookTickInfos().GetTicks().ToList();

        Assert.Equal(2, ticks.Count);
    }

    // ---------------------------------------------------------------
    // CanMatch / CanFullyMatch
    // ---------------------------------------------------------------

    [Fact]
    public void CanMatch_OnAnEmptyBook_ReturnsFalseForBothSides()
    {
        using var book = new OB();

        Assert.False(book.CanMatch(Side.Buy, new Price(100m)));
        Assert.False(book.CanMatch(Side.Sell, new Price(100m)));
    }

    [Fact]
    public void CanMatch_ReflectsWhetherPriceCrossesTheOppositeSide()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(5), Side.Sell, OrderType.GoodTillCancel));

        Assert.True(book.CanMatch(Side.Buy, new Price(100m)));
        Assert.False(book.CanMatch(Side.Buy, new Price(99m)));
        Assert.False(book.CanMatch(Side.Sell, new Price(100m))); // no resting bids at all
    }

    [Fact]
    public void CanFullyMatch_OnlyCountsLevelsWithinTheRequestedPriceAndSide()
    {
        using var book = new OB();
        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(3), Side.Sell, OrderType.GoodTillCancel));
        book.AddOrder(new Order(new OrderId(2), new Price(101m), new Quantity(2), Side.Sell, OrderType.GoodTillCancel));

        Assert.True(book.CanFullyMatch(Side.Buy, new Price(101m), new Quantity(5))); // both levels: 3 + 2
        Assert.False(book.CanFullyMatch(Side.Buy, new Price(101m), new Quantity(6))); // exceeds total available
        Assert.False(book.CanFullyMatch(Side.Buy, new Price(100m), new Quantity(4))); // level 101 is above the requested limit price, so only 3 counts
    }

    // ---------------------------------------------------------------
    // Dispose
    // ---------------------------------------------------------------

    [Fact]
    public void Dispose_StopsThePruningThreadPromptly_WithoutWaitingForMidnight()
    {
        var book = new OB();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        book.Dispose();

        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds < 5000);
    }

    // ---------------------------------------------------------------
    // Strategy injection (IPriceLadder) - OrderBook only depends on the
    // interface, so a custom/future ladder implementation should be usable
    // as a straight drop-in replacement for the default tree-based one.
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_CustomPriceLadderFactory_IsUsedForBothSidesAndDrivesMatching()
    {
        var createdLadders = new List<CountingPriceLadder>();
        using var book = new OB(TimeProvider.System, comparer =>
        {
            var ladder = new CountingPriceLadder(comparer);
            createdLadders.Add(ladder);
            return ladder;
        });

        book.AddOrder(new Order(new OrderId(1), new Price(100m), new Quantity(10), Side.Buy, OrderType.GoodTillCancel));
        var trades = book.AddOrder(new Order(new OrderId(2), new Price(100m), new Quantity(10), Side.Sell, OrderType.GoodTillCancel));

        Assert.Equal(2, createdLadders.Count); // one factory invocation per side (bids, asks)
        Assert.Single(trades); // matching still works end-to-end through the injected strategy
        Assert.True(createdLadders.Sum(l => l.GetOrCreateLevelCalls) > 0); // proves the injected instances were actually used, not bypassed
    }

    // Thin spy wrapping the default implementation, purely to prove OrderBook
    // drives whatever IPriceLadder it's given rather than a concrete type.
    private sealed class CountingPriceLadder(IComparer<Price> comparer) : IPriceLadder
    {
        private readonly TreePriceLadder _inner = new(comparer);
        public int GetOrCreateLevelCalls { get; private set; }

        public int Count => _inner.Count;
        public Price? WorstPrice => _inner.WorstPrice;

        public bool TryGetLevel(Price price, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PriceLevel? level) =>
            _inner.TryGetLevel(price, out level);

        public PriceLevel GetOrCreateLevel(Price price)
        {
            GetOrCreateLevelCalls++;
            return _inner.GetOrCreateLevel(price);
        }

        public void RemoveLevelIfEmpty(Price price, PriceLevel level) => _inner.RemoveLevelIfEmpty(price, level);
        public KeyValuePair<Price, PriceLevel> First() => _inner.First();
        public IEnumerator<KeyValuePair<Price, PriceLevel>> GetEnumerator() => _inner.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
