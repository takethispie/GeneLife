namespace GeneLife.Common.Components.Containers;

public struct LiquidContainer
{
    /// <summary>
    /// amount in CC (cubic centimeters)
    /// </summary>
    public int MaxAmount;
    public int CurrentAmount;
    public readonly string Type;
}