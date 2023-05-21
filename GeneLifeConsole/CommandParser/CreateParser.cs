using GeneLife;
using GeneLife.Genetic.GeneticTraits;

namespace GeneLifeConsole.CommandParser;

internal class CreateParser
{
    public static string Parse(GeneLifeSimulation simulation, string[] text)
    {
        return text switch
        {
            ["male", var rest] => CreateNPCWithMinimumAge(simulation, Sex.Male, rest),
            ["female", var rest] => CreateNPCWithMinimumAge(simulation, Sex.Female, rest),
            ["male", ..] => CreateNPC(simulation, Sex.Male),
            ["female", ..] => CreateNPC(simulation, Sex.Female),
            _ => "unknown create command"
        };
    }

    private static string CreateNPC(GeneLifeSimulation simulation, Sex sex) => simulation.AddNPC(sex);

    private static string CreateNPCWithMinimumAge(GeneLifeSimulation simulation, Sex sex, string startAge) => 
        int.TryParse(startAge, out var age) ? simulation.AddNPC(sex, age) : "Could not parse the age parameter";
}