namespace Genelife.Components.Consumption;

public record struct NutritionState
{
    public int Balance;
    public int ConsecutivePoorDays;
    public long LastMealTick;
}
