namespace Genelife.Values;

/// <summary>
/// Exact monetary value stored as integer cents. All gameplay money movements
/// use this type so payroll, rent, taxes and ledgers reconcile without the
/// rounding drift of floating-point balances.
/// </summary>
public readonly record struct Money(long Cents) : IComparable<Money>
{
    public static readonly Money Zero = new(0);

    public decimal Units => Cents / 100m;
    public bool IsNegative => Cents < 0;
    public bool IsZero => Cents == 0;
    public bool IsPositive => Cents > 0;

    public static Money FromUnits(decimal units) =>
        new((long)Math.Round(units * 100m, MidpointRounding.AwayFromZero));

    public static Money FromCents(long cents) => new(cents);

    public Money Multiply(decimal factor) =>
        new((long)Math.Round(Cents * factor, MidpointRounding.AwayFromZero));

    public Money Min(Money other) => Cents <= other.Cents ? this : other;
    public Money Max(Money other) => Cents >= other.Cents ? this : other;

    public static Money operator +(Money a, Money b) => new(checked(a.Cents + b.Cents));
    public static Money operator -(Money a, Money b) => new(checked(a.Cents - b.Cents));
    public static Money operator -(Money a) => new(checked(-a.Cents));
    public static Money operator *(Money a, int factor) => new(checked(a.Cents * factor));
    public static Money operator *(Money a, decimal factor) => a.Multiply(factor);
    public static bool operator >(Money a, Money b) => a.Cents > b.Cents;
    public static bool operator <(Money a, Money b) => a.Cents < b.Cents;
    public static bool operator >=(Money a, Money b) => a.Cents >= b.Cents;
    public static bool operator <=(Money a, Money b) => a.Cents <= b.Cents;

    public int CompareTo(Money other) => Cents.CompareTo(other.Cents);

    public override string ToString() => Units.ToString("0.00");
}
