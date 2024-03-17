using GeneLife.Core.Components;
using GeneLife.Core.Components.Buildings;
using GeneLife.Knowledge.Components;

namespace Genelife.Web.DTOs;

public class Building
{
    public int Id { get; }
    public string Adress { get; }
    public string Position { get; }
    public string Type { get; }

    public Building(int id, Adress adress, Position position, Shop _)
    {
        Id = id;
        Adress = adress.Full();
        Position = position.Coordinates.ToString();
        Type = "shop";
    }

    public Building(int id, Adress adress, Position position, School _)
    {
        Id = id;
        Adress = adress.Full();
        Position = position.Coordinates.ToString();
        Type = "school";
    }

    public Building(int id, Adress adress, Position position, HouseHold _)
    {
        Id = id;
        Adress = adress.Full();
        Position = position.Coordinates.ToString();
        Type = "household";
    }
}