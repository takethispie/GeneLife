using GeneLife;

namespace GeneLifeConsole.CommandParser;

public class CommandParser
{
    private readonly GeneLifeSimulation _simulation;
    private readonly CreateParser _createParser = new();

    public CommandParser(GeneLifeSimulation simulation)
    {
        _simulation = simulation;
    }

    public string Parse(string command) =>
        command.ToLower().Split(" ") switch
        {
            ["create", ..] => _createParser.Parse(_simulation, command.ToLower().Split(" ").Skip(1).ToArray()),
            _ => "Unknow Command"
        };
}