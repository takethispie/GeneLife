namespace GeneLife.Athena.Core.Objectives;

public record Eat(int Priority, string Name = "Eat") : IObjective;