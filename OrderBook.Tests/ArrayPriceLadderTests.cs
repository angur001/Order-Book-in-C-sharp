using OrderBook;
using OrderBook.Classes;
using OrderBook.Interfaces;

namespace OrderBook.Tests;

public class ArrayPriceLadderTests : PriceLadderContractTests
{
    // Wide enough range/tick size to cover every price the shared contract
    // tests use (100-110, in increments as fine as 2).
    private static readonly Price MinPrice = new(0m);
    private static readonly Price MaxPrice = new(200m);
    private const decimal TickSize = 1m;

    protected override IPriceLadder CreateAscendingLadder() =>
        new ArrayPriceLadder(Comparer<Price>.Create((x, y) => x.Value.CompareTo(y.Value)), MinPrice, MaxPrice, TickSize);

    protected override IPriceLadder CreateDescendingLadder() =>
        new ArrayPriceLadder(Comparer<Price>.Create((x, y) => y.Value.CompareTo(x.Value)), MinPrice, MaxPrice, TickSize);

    // ---------------------------------------------------------------
    // Constructor validation - the trade-off this strategy makes for O(1)
    // access is that its range and granularity have to be nailed down and
    // valid up front, so bad configuration should fail fast and loudly.
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_NonPositiveTickSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ArrayPriceLadder(Comparer<Price>.Create((x, y) => x.Value.CompareTo(y.Value)), new Price(0m), new Price(10m), 0m));
    }

    [Fact]
    public void Constructor_MaxPriceNotGreaterThanMinPrice_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ArrayPriceLadder(Comparer<Price>.Create((x, y) => x.Value.CompareTo(y.Value)), new Price(10m), new Price(10m), 1m));
    }

    [Fact]
    public void Constructor_RangeNotAnExactMultipleOfTickSize_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ArrayPriceLadder(Comparer<Price>.Create((x, y) => x.Value.CompareTo(y.Value)), new Price(0m), new Price(10m), 3m));
    }

    // ---------------------------------------------------------------
    // Out-of-range / off-grid prices
    // ---------------------------------------------------------------

    [Fact]
    public void GetOrCreateLevel_PriceBelowConfiguredRange_Throws()
    {
        var ladder = CreateAscendingLadder();

        Assert.Throws<ArgumentOutOfRangeException>(() => ladder.GetOrCreateLevel(new Price(MinPrice.Value - 1m)));
    }

    [Fact]
    public void GetOrCreateLevel_PriceAboveConfiguredRange_Throws()
    {
        var ladder = CreateAscendingLadder();

        Assert.Throws<ArgumentOutOfRangeException>(() => ladder.GetOrCreateLevel(new Price(MaxPrice.Value + 1m)));
    }

    [Fact]
    public void GetOrCreateLevel_PriceNotAlignedToTickSize_Throws()
    {
        var ladder = CreateAscendingLadder();

        Assert.Throws<ArgumentOutOfRangeException>(() => ladder.GetOrCreateLevel(new Price(100.5m)));
    }

    [Fact]
    public void TryGetLevel_PriceOutsideConfiguredRange_ReturnsFalseInsteadOfThrowing()
    {
        var ladder = CreateAscendingLadder();

        Assert.False(ladder.TryGetLevel(new Price(MaxPrice.Value + 1m), out _));
        Assert.False(ladder.TryGetLevel(new Price(MinPrice.Value - 1m), out _));
    }

    [Fact]
    public void TryGetLevel_PriceNotAlignedToTickSize_ReturnsFalseInsteadOfThrowing()
    {
        var ladder = CreateAscendingLadder();
        ladder.GetOrCreateLevel(new Price(100m));

        Assert.False(ladder.TryGetLevel(new Price(100.5m), out _));
    }

    [Fact]
    public void GetOrCreateLevel_AtTheExactRangeBoundaries_Succeeds()
    {
        var ladder = CreateAscendingLadder();

        ladder.GetOrCreateLevel(MinPrice);
        ladder.GetOrCreateLevel(MaxPrice);

        Assert.Equal(2, ladder.Count);
        Assert.True(ladder.TryGetLevel(MinPrice, out _));
        Assert.True(ladder.TryGetLevel(MaxPrice, out _));
    }
}
