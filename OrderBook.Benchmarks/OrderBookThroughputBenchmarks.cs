using BenchmarkDotNet.Attributes;
using OrderBook.Classes;
using OrderBook.Enums;
using OB = OrderBook.OrderBook;

namespace OrderBook.Benchmarks;

// End-to-end AddOrder traffic through the real OrderBook (resting, matching,
// and the level churn that comes with both) - the two IPriceLadder
// strategies compared under realistic usage rather than in isolation.
[MemoryDiagnoser]
public class OrderBookThroughputBenchmarks
{
    private const decimal MinPrice = 0m;
    private const decimal MaxPrice = 1_000m;

    [Params(1_000, 10_000)]
    public int OrderCount;

    private (Side Side, Price Price, Quantity Quantity)[] _plan = null!;
    private OB _treeBook = null!;
    private OB _arrayBook = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var random = new Random(42);
        _plan = new (Side, Price, Quantity)[OrderCount];
        for (var i = 0; i < OrderCount; i++)
        {
            var side = random.Next(2) == 0 ? Side.Buy : Side.Sell;
            var price = new Price(100 + random.Next(0, 200));
            var quantity = new Quantity((uint)random.Next(1, 50));
            _plan[i] = (side, price, quantity);
        }
    }

    [IterationSetup(Target = nameof(Tree))]
    public void SetupTree() =>
        _treeBook = new OB(TimeProvider.System, comparer => new TreePriceLadder(comparer));

    [IterationSetup(Target = nameof(Array))]
    public void SetupArray() =>
        _arrayBook = new OB(
            TimeProvider.System,
            comparer => new ArrayPriceLadder(comparer, new Price(MinPrice), new Price(MaxPrice), 1m));

    [IterationCleanup(Target = nameof(Tree))]
    public void CleanupTree() => _treeBook.Dispose();

    [IterationCleanup(Target = nameof(Array))]
    public void CleanupArray() => _arrayBook.Dispose();

    [Benchmark(Baseline = true)]
    public void Tree()
    {
        for (var i = 0; i < _plan.Length; i++)
        {
            var (side, price, quantity) = _plan[i];
            _treeBook.AddOrder(new Order(new OrderId((uint)i), price, quantity, side, OrderType.GoodTillCancel));
        }
    }

    [Benchmark]
    public void Array()
    {
        for (var i = 0; i < _plan.Length; i++)
        {
            var (side, price, quantity) = _plan[i];
            _arrayBook.AddOrder(new Order(new OrderId((uint)i), price, quantity, side, OrderType.GoodTillCancel));
        }
    }
}
