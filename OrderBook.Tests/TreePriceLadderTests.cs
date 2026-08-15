using OrderBook;
using OrderBook.Classes;
using OrderBook.Interfaces;

namespace OrderBook.Tests;

public class TreePriceLadderTests : PriceLadderContractTests
{
    protected override IPriceLadder CreateAscendingLadder() =>
        new TreePriceLadder(Comparer<Price>.Create((x, y) => x.Value.CompareTo(y.Value)));

    protected override IPriceLadder CreateDescendingLadder() =>
        new TreePriceLadder(Comparer<Price>.Create((x, y) => y.Value.CompareTo(x.Value)));
}
