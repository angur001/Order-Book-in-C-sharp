using BenchmarkDotNet.Attributes;
using OrderBook.Classes;
using OrderBook.Interfaces;

namespace OrderBook.Benchmarks;

// Full walk of a densely-packed ladder (every slot in range is occupied),
// where the array ladder's contiguous backing store should have the edge.
[MemoryDiagnoser]
public class DenseEnumerationBenchmarks
{
    [Params(100, 1_000, 10_000)]
    public int LevelCount;

    private IPriceLadder _treeLadder = null!;
    private IPriceLadder _arrayLadder = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _treeLadder = PriceLadderFactory.CreateTree();
        _arrayLadder = PriceLadderFactory.CreateArray(LevelCount);

        foreach (var price in PriceLadderFactory.SequentialPrices(LevelCount))
        {
            _treeLadder.GetOrCreateLevel(price);
            _arrayLadder.GetOrCreateLevel(price);
        }
    }

    [Benchmark(Baseline = true)]
    public decimal Tree()
    {
        decimal sum = 0;
        foreach (var (price, _) in _treeLadder) sum += price.Value;
        return sum;
    }

    [Benchmark]
    public decimal Array()
    {
        decimal sum = 0;
        foreach (var (price, _) in _arrayLadder) sum += price.Value;
        return sum;
    }
}

// The array ladder's documented trade-off: enumeration walks every
// configured slot whether occupied or not (O(capacity)), while the tree
// ladder only ever walks occupied levels (O(occupied)). This fixes a small,
// constant number of occupied levels and grows only the array's configured
// price-range capacity around them, to isolate that cost.
[MemoryDiagnoser]
public class SparseEnumerationBenchmarks
{
    private const int OccupiedLevelCount = 50;

    [Params(1_000, 100_000)]
    public int RangeCapacity;

    private IPriceLadder _treeLadder = null!;
    private IPriceLadder _arrayLadder = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _treeLadder = PriceLadderFactory.CreateTree();
        _arrayLadder = PriceLadderFactory.CreateArray(RangeCapacity);

        var step = RangeCapacity / OccupiedLevelCount;
        for (var i = 0; i < OccupiedLevelCount; i++)
        {
            var price = new Price(i * step);
            _treeLadder.GetOrCreateLevel(price);
            _arrayLadder.GetOrCreateLevel(price);
        }
    }

    [Benchmark(Baseline = true)]
    public decimal Tree()
    {
        decimal sum = 0;
        foreach (var (price, _) in _treeLadder) sum += price.Value;
        return sum;
    }

    [Benchmark]
    public decimal Array()
    {
        decimal sum = 0;
        foreach (var (price, _) in _arrayLadder) sum += price.Value;
        return sum;
    }
}
