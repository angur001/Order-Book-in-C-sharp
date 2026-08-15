using BenchmarkDotNet.Attributes;
using OrderBook.Classes;
using OrderBook.Interfaces;

namespace OrderBook.Benchmarks;

// TryGetLevel against a fully-populated, densely-packed ladder, looked up in
// shuffled (non-sequential) order so no branch-prediction/cache pattern from
// insertion order carries over.
[MemoryDiagnoser]
public class LookupBenchmarks
{
    [Params(100, 1_000, 10_000)]
    public int LevelCount;

    private Price[] _lookupPrices = null!;
    private IPriceLadder _treeLadder = null!;
    private IPriceLadder _arrayLadder = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _lookupPrices = PriceLadderFactory.ShuffledPrices(LevelCount);

        _treeLadder = PriceLadderFactory.CreateTree();
        _arrayLadder = PriceLadderFactory.CreateArray(LevelCount);

        foreach (var price in PriceLadderFactory.SequentialPrices(LevelCount))
        {
            _treeLadder.GetOrCreateLevel(price);
            _arrayLadder.GetOrCreateLevel(price);
        }
    }

    [Benchmark(Baseline = true)]
    public int Tree()
    {
        var found = 0;
        foreach (var price in _lookupPrices)
        {
            if (_treeLadder.TryGetLevel(price, out _)) found++;
        }

        return found;
    }

    [Benchmark]
    public int Array()
    {
        var found = 0;
        foreach (var price in _lookupPrices)
        {
            if (_arrayLadder.TryGetLevel(price, out _)) found++;
        }

        return found;
    }
}
