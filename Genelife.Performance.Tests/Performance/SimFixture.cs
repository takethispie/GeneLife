using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Enums;

namespace Genelife.UnitTests.Performance;

public static class SimFixture
{
    public static void PopulateSynthetic(World world, int count, int seed = 42)
    {
        var rng = new Random(seed);
        var needsValues = new float[] { 10f, 25f, 50f, 75f, 100f };

        for (int i = 0; i < count; i++)
        {
            world.Create(
                new GenName($"Sim_{i}"),
                CreateNeeds(rng, needsValues),
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(rng.Next(18, 80))
            );
        }
    }

    public static void PopulateRealistic(World world, int count, int seed = 42)
    {
        var rng = new Random(seed);
        var actions = new[]
        {
            ActionType.Eating, ActionType.Sleeping, ActionType.Showering,
            ActionType.UsingToilet, ActionType.Idle
        };
        var durations = new float[] { 3f, 5f, 2f, 1.5f, 0f };

        for (int i = 0; i < count; i++)
        {
            var actionIdx = rng.Next(actions.Length);
            world.Create(
                new GenName($"Sim_{i}"),
                CreateRealisticNeeds(rng, count),
                new CurrentAction(actions[actionIdx], durations[actionIdx]),
                new Alive(rng.Next(18, 80))
            );
        }
    }

    private static Needs CreateNeeds(Random rng, float[] pool)
    {
        return new Needs
        {
            Hunger = pool[rng.Next(pool.Length)],
            Energy = pool[rng.Next(pool.Length)],
            Hygiene = pool[rng.Next(pool.Length)],
            Bladder = pool[rng.Next(pool.Length)]
        };
    }

    private static Needs CreateRealisticNeeds(Random rng, int totalPopulation)
    {
        double hungryFraction = 0.08;
        double tiredFraction = 0.08;
        double dirtyFraction = 0.07;
        double bathroomFraction = 0.07;

        return new Needs
        {
            Hunger = rng.NextDouble() < hungryFraction ? rng.Next(0, 30) : rng.Next(50, 101),
            Energy = rng.NextDouble() < tiredFraction ? rng.Next(0, 30) : rng.Next(50, 101),
            Hygiene = rng.NextDouble() < dirtyFraction ? rng.Next(0, 30) : rng.Next(50, 101),
            Bladder = rng.NextDouble() < bathroomFraction ? rng.Next(0, 30) : rng.Next(50, 101)
        };
    }
}