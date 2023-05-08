using Arch.Core;
using Arch.Core.Extensions;
using GeneLife.Generators;
using GeneLife.Genetic.GeneticTraits;

namespace GeneLife;

public class GeneLife
{
    public World Main { get; init; }

    public GeneLife()
    {
        Main = World.Create();
    }

    public void AddNewHuman(Sex sex)
    {
        PersonGenerator.CreatePure(Main, Sex.Male);
    }
}