using GeneLife;
using GeneLife.Genetic.GeneticTraits;

namespace GeneLifeConsole.CommandParser;

public class CreateParser
{
    public string Parse(GeneLifeSimulation simulation, string[] text)
    {
        return text switch
        {
            ["npc", "male"] => CreateNPC(simulation, Sex.Male),
            ["npc", "female"] => CreateNPC(simulation, Sex.Female),
            _ => "unknown create command"
        };
    }

    public string CreateNPC(GeneLifeSimulation simulation, Sex sex) => simulation.AddNPC(Sex.Male);
}