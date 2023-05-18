namespace GeneLife.Core.Objectives;

public record Drink(int Priority, string Name = "Drink") : IObjective;