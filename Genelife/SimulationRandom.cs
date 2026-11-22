namespace Genelife;

/// <summary>
/// Deterministic random streams derived from a world seed. Systems must not use
/// Random.Shared; asking for the same stream name always returns the same
/// sequence for a given seed.
/// </summary>
public sealed class SimulationRandom
{
    private readonly int seed;

    public SimulationRandom(int seed)
    {
        this.seed = seed;
    }

    public int Seed => seed;

    public Random For(string streamName)
    {
        return new Random(unchecked(seed * 397 ^ StableHash(streamName)));
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return (int)hash;
        }
    }
}
