using OrderBook;

namespace OrderBook.Tests;

public class DomainTypesTests
{
    [Fact]
    public void Quantity_Addition_SumsValues()
    {
        var a = new Quantity(3);
        var b = new Quantity(4);

        Assert.Equal(new Quantity(7), a + b);
    }

    [Fact]
    public void Quantity_Subtraction_SubtractsValues()
    {
        var a = new Quantity(10);
        var b = new Quantity(4);

        Assert.Equal(new Quantity(6), a - b);
    }

    [Fact]
    public void Quantity_Equality_IsValueBased()
    {
        Assert.Equal(new Quantity(5), new Quantity(5));
        Assert.NotEqual(new Quantity(5), new Quantity(6));
    }

    [Fact]
    public void Price_InvalidPrice_IsNegativeOne()
    {
        Assert.Equal(-1m, Price.InvalidPrice.Value);
    }

    [Fact]
    public void Price_Equality_IsValueBased()
    {
        Assert.Equal(new Price(100.5m), new Price(100.5m));
        Assert.NotEqual(new Price(100.5m), new Price(100.6m));
    }

    [Fact]
    public void OrderId_Equality_IsValueBased()
    {
        Assert.Equal(new OrderId(1), new OrderId(1));
        Assert.NotEqual(new OrderId(1), new OrderId(2));
    }

    // Quantity subtraction is unchecked: nothing in the struct itself stops the
    // caller from underflowing an unsigned value. Callers (e.g. Order.Fill) are
    // responsible for guarding against this - this test documents that the
    // primitive itself provides no protection.
    [Fact]
    public void Quantity_Subtraction_UnderflowWrapsInsteadOfThrowing()
    {
        var smaller = new Quantity(1);
        var bigger = new Quantity(2);

        var result = smaller - bigger;

        Assert.Equal(uint.MaxValue, result.Value);
    }
}
