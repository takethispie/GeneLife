using GeneLife;
using GeneLife.Core.Commands;
using GeneLife.Core.Items;

namespace GeneLifeConsole.CommandParser;

public class GiveParser
{
    public static string Parse(GeneLifeSimulation simulation, string[] text)
    {
        if (!int.TryParse(text[0], out var itemId)) return "Unknown Command";
        var entityNames = text[1].Split(',');
        if (entityNames.Length < 2) return "Incorrect args";
        var command = itemId switch
        {
            1 => new GiveCommand
            {
                Item = new Item { Id = 1, Type = ItemType.Food }, 
                TargetFirstName = entityNames[0],
                TargetLastName = entityNames[1]
            },
            2 => new GiveCommand
            {
                Item = new Item { Id = 2, Type = ItemType.Drink }, 
                TargetFirstName = entityNames[0],
                TargetLastName = entityNames[1]
            },
            _ => null
        };
        if (command == null) return "Unknown Command";
        simulation.SendCommand(command);
        return "";
    }
}