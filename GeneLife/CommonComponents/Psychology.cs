namespace GeneLife.CommonComponents;

public struct Psychology
{
    /// <summary>
    /// lower than 0.5 => sad, >= 0.5 => average / happy
    /// </summary>
    public float EmotionalBalance;

    public Psychology(float emotionalBalance)
    {
        EmotionalBalance = emotionalBalance;
    }
}