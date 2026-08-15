using OrderBook.Classes;
using OrderBook.Interfaces;

namespace OrderBook.Benchmarks;

// Shared setup helpers for the ladder benchmarks: both implementations are
// built through the same ascending comparer and price sets so the two are
// compared on identical inputs.
internal static class PriceLadderFactory
{
    public static IComparer<Price> AscendingComparer { get; } =
        Comparer<Price>.Create((x, y) => x.Value.CompareTo(y.Value));

    public static IPriceLadder CreateTree() => new TreePriceLadder(AscendingComparer);

    // maxPrice = capacity gives slots for prices 0..capacity inclusive, at a
    // tick size of 1, which is enough headroom for every price this factory hands out.
    public static IPriceLadder CreateArray(int capacity) =>
        new ArrayPriceLadder(AscendingComparer, new Price(0m), new Price(capacity), 1m);

    public static Price[] SequentialPrices(int count) =>
        Enumerable.Range(0, count).Select(i => new Price(i)).ToArray();

    public static Price[] ShuffledPrices(int count, int seed = 42)
    {
        var prices = SequentialPrices(count);
        var random = new Random(seed);
        for (var i = prices.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (prices[i], prices[j]) = (prices[j], prices[i]);
        }

        return prices;
    }
}
