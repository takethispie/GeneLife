namespace GeneLife.Common.Components;

public struct Psychology
{
    /// <summary>
    /// lower than 50 => sad, >= 50 => average / happy
    /// </summary>
    public int EmotionalBalance;
    
    /// <summary>
    /// starts at 0, 60+ reduce performance and capacity to make rational choices, 100 introduce a heart attack risk
    /// </summary>
    public int Stress;

    public Psychology()
    {
        EmotionalBalance = 60;
        Stress = 0;
    }
    
    public Psychology(int emotionalBalance)
    {
        EmotionalBalance = emotionalBalance;
        Stress = 0;
    }
}