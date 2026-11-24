using Genelife.Values;

namespace Genelife.UnitTests.Values;

public class MoneyTests
{
    [Fact]
    public void FromUnits_RoundsToCents()
    {
        Assert.Equal(12345, Money.FromUnits(123.45m).Cents);
        Assert.Equal(12346, Money.FromUnits(123.455m).Cents);
        Assert.Equal(-12345, Money.FromUnits(-123.45m).Cents);
    }

    [Fact]
    public void Multiply_RoundsAwayFromZero()
    {
        Assert.Equal(5000, Money.FromUnits(1000m).Multiply(0.05m).Cents);
        Assert.Equal(12000, Money.FromUnits(1000m).Multiply(0.12m).Cents);
        Assert.Equal(2, Money.FromCents(3).Multiply(0.5m).Cents);
    }

    [Fact]
    public void Arithmetic_IsExact()
    {
        var a = Money.FromUnits(0.10m);
        var b = Money.FromUnits(0.20m);
        Assert.Equal(Money.FromUnits(0.30m), a + b);
        Assert.Equal(Money.FromUnits(0.10m), b - a);
        Assert.True(b > a);
        Assert.Equal(Money.FromUnits(0.10m), a.Min(b));
    }
}
