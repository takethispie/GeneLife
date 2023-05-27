namespace GeneLife.Athena.Core.Objectives;

public record Sleep(int Priority, string Name = "Sleep"): IObjective;