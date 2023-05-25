namespace GeneLife.Core.Components;

public struct Lifespan
{
    public TimeSpan Age;
    public TimeSpan MaxAge;

    public Lifespan(TimeSpan maxAge)
    {
        Age = TimeSpan.Zero;
        MaxAge = maxAge;
    }
    public Lifespan(TimeSpan age, TimeSpan maxAge)
    {
        Age = age;
        MaxAge = maxAge;
    }
}