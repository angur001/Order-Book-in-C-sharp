using BenchmarkDotNet.Attributes;
using OrderBook.Classes;
using OrderBook.Interfaces;

namespace OrderBook.Benchmarks;

// Repeatedly creates and immediately empties the level at a single fixed
// price - the worst-case churn scenario for the level at the touch price in
// an active book, and the scenario InsertBenchmarks/RemoveBenchmarks don't
// exercise since they only ever grow into distinct, never-revisited levels.
// This is what actually proves (or disproves) the PriceLevel pool: without
// it, every churn iteration after the first allocates a new PriceLevel; with
// it, only the very first iteration should allocate one.
[MemoryDiagnoser]
public class LevelChurnBenchmarks
{
    [Params(100, 1_000, 10_000)]
    public int ChurnCount;

    private static readonly Price Price = new(0m);

    private IPriceLadder _treeLadder = null!;
    private IPriceLadder _arrayLadder = null!;

    [IterationSetup(Target = nameof(Tree))]
    public void SetupTree() => _treeLadder = PriceLadderFactory.CreateTree();

    [IterationSetup(Target = nameof(Array))]
    public void SetupArray() => _arrayLadder = PriceLadderFactory.CreateArray(1);

    [Benchmark(Baseline = true)]
    public void Tree()
    {
        for (var i = 0; i < ChurnCount; i++)
        {
            var level = _treeLadder.GetOrCreateLevel(Price);
            _treeLadder.RemoveLevelIfEmpty(Price, level);
        }
    }

    [Benchmark]
    public void Array()
    {
        for (var i = 0; i < ChurnCount; i++)
        {
            var level = _arrayLadder.GetOrCreateLevel(Price);
            _arrayLadder.RemoveLevelIfEmpty(Price, level);
        }
    }
}
