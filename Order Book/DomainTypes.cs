namespace OrderBook;

public readonly record struct OrderId(uint Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct Quantity(uint Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct Price(decimal Value)
{
    public static Price InvalidPrice { get; } = new(-1m);

    public override string ToString() => Value.ToString();
}