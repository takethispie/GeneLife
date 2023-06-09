using GeneLife;
using GeneLife.Core.Data;
using GeneLife.Genetic.GeneticTraits;

namespace GeneLifeConsole.CommandParser;

internal static class ChangeParser
{
    public static string Parse(GeneLifeSimulation simulation, string[] text)
    {
        return text switch
        {
            ["ticksperday", var value] => ChangeTicksPerDay(simulation, value),
            _ => "unknown create command"
        };
    }
    
    private static string ChangeTicksPerDay(GeneLifeSimulation simulation, string value)
    {
        if (!int.TryParse(value, out var ticks)) return "Could not parse the age parameter";
        Constants.TicksPerDay = ticks;
        return $"Changed ticks per day to {ticks}";
    }
}