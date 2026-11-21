using Arch.Core;

namespace Genelife.UnitTests;

/// <summary>
/// Helper to create valid Arch entities for tests.
/// </summary>
public static class TestWorld
{
    /// <summary>
    /// Creates a fresh World and returns a factory that mints entities from it.
    /// The World must be kept alive for the lifetime of the entities.
    /// </summary>
    public static (World World, Func<Entity> CreateEntity) New()
    {
        var world = World.Create();
        return (world, () => world.Create());
    }
}