using BenchmarkDotNet.Attributes;
using OrderBook.Classes;
using OrderBook.Interfaces;

namespace OrderBook.Benchmarks;

// GetOrCreateLevel on a ladder that starts empty every iteration - the array
// ladder's O(1) index-into-slot insert against the tree ladder's O(log n)
// red-black-tree insert.
[MemoryDiagnoser]
public class InsertBenchmarks
{
    [Params(100, 1_000, 10_000)]
    public int LevelCount;

    private Price[] _prices = null!;
    private IPriceLadder _treeLadder = null!;
    private IPriceLadder _arrayLadder = null!;

    [GlobalSetup]
    public void GlobalSetup() => _prices = PriceLadderFactory.SequentialPrices(LevelCount);

    [IterationSetup(Target = nameof(Tree))]
    public void SetupTree() => _treeLadder = PriceLadderFactory.CreateTree();

    [IterationSetup(Target = nameof(Array))]
    public void SetupArray() => _arrayLadder = PriceLadderFactory.CreateArray(LevelCount);

    [Benchmark(Baseline = true)]
    public void Tree()
    {
        foreach (var price in _prices)
        {
            _treeLadder.GetOrCreateLevel(price);
        }
    }

    [Benchmark]
    public void Array()
    {
        foreach (var price in _prices)
        {
            _arrayLadder.GetOrCreateLevel(price);
        }
    }
}
