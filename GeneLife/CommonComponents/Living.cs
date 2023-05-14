namespace GeneLife.CommonComponents;

public struct Living
{
    public int MaxHitPoints;
    public int HitPoints;
    public int Hunger;
    public int Thirst;
    public int MaxStamina;
    public int Stamina;

    /// <summary>
    /// constructor without arguments sets default values
    /// </summary>
    public Living()
    {
        MaxHitPoints = 10;
        HitPoints = MaxHitPoints;
        Hunger = 10;
        Thirst = 20;
        MaxStamina = 10;
        Stamina = MaxStamina;
    }

    public Living(int maxHitPoints, int maxStamina, int hunger, int thirst)
    {
        MaxHitPoints = maxHitPoints;
        HitPoints = MaxHitPoints;
        MaxStamina = maxStamina;
        Stamina = MaxStamina;
        Hunger = hunger;
        Thirst = thirst;
    }
}