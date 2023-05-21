namespace GeneLife.Core.Objectives;

public record BuyItem(int Priority, int ItemId, string Name = "BuyItem") : IObjective;