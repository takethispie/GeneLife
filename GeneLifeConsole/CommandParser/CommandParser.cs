using GeneLife;

namespace GeneLifeConsole.CommandParser;

public sealed class CommandParser
{
    private readonly GeneLifeSimulation _simulation;

    public CommandParser(GeneLifeSimulation simulation)
    {
        _simulation = simulation;
    }

    public string Parse(string command) =>
        command.ToLower().Split(" ") switch
        {
            ["create", ..] => CreateParser.Parse(_simulation, skipHead(command)),
            ["change", ..] => ChangeParser.Parse(_simulation, skipHead(command)),
            ["give", ..] => GiveParser.Parse(_simulation, skipHead(command)),
            _ => "Unknow Command"
        };

    private static string[] skipHead(string command) => command.ToLower().Split(" ").Skip(1).ToArray();
}