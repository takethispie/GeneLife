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
            ["create", ..] => CreateParser.Parse(_simulation, skipHead(command)),
            ["change", ..] => ChangeParser.Parse(_simulation, skipHead(command)),
            _ => "Unknow Command"
        };

    public string[] skipHead(string command) => command.ToLower().Split(" ").Skip(1).ToArray();
}