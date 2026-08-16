namespace OrderBook.Classes;

// Minimal free-list pool
// used in combination with _mutex inside the order book
public sealed class ObjectPool<T> where T : class
{
    private readonly Stack<T> _items = new();
    private readonly Func<T> _factory;
    private readonly Action<T> _reset;

    public ObjectPool(Func<T> factory, Action<T> reset)
    {
        _factory = factory;
        _reset = reset;
    }

    public T Rent() => _items.Count > 0 ? _items.Pop() : _factory();

    public void Return(T item)
    {
        _reset(item);
        _items.Push(item);
    }
}
