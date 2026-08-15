using BenchmarkDotNet.Attributes;
using OrderBook.Classes;
using OrderBook.Interfaces;

namespace OrderBook.Benchmarks;

// RemoveLevelIfEmpty walking every price in ascending order, immediately
// after the level was created (so it's still empty and eligible for
// removal) - re-filled fresh every iteration since the benchmark drains the
// ladder as it runs.
[MemoryDiagnoser]
public class RemoveBenchmarks
{
    [Params(100, 1_000, 10_000)]
    public int LevelCount;

    private Price[] _prices = null!;
    private IPriceLadder _treeLadder = null!;
    private IPriceLadder _arrayLadder = null!;

    [GlobalSetup]
    public void GlobalSetup() => _prices = PriceLadderFactory.SequentialPrices(LevelCount);

    [IterationSetup(Target = nameof(Tree))]
    public void SetupTree()
    {
        _treeLadder = PriceLadderFactory.CreateTree();
        foreach (var price in _prices) _treeLadder.GetOrCreateLevel(price);
    }

    [IterationSetup(Target = nameof(Array))]
    public void SetupArray()
    {
        _arrayLadder = PriceLadderFactory.CreateArray(LevelCount);
        foreach (var price in _prices) _arrayLadder.GetOrCreateLevel(price);
    }

    [Benchmark(Baseline = true)]
    public void Tree()
    {
        foreach (var price in _prices)
        {
            _treeLadder.TryGetLevel(price, out var level);
            _treeLadder.RemoveLevelIfEmpty(price, level!);
        }
    }

    [Benchmark]
    public void Array()
    {
        foreach (var price in _prices)
        {
            _arrayLadder.TryGetLevel(price, out var level);
            _arrayLadder.RemoveLevelIfEmpty(price, level!);
        }
    }
}
