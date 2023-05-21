namespace GeneLife.Core.Components.Characters;

public struct Living
{
    public int Damage;
    
    /// <summary>
    /// goes down with time, the lower it is the higher the hunger is
    /// </summary>
    public int Hunger;
    
    /// <summary>
    /// goes down with time, the lower it is the higher the thirst is
    /// </summary>
    public int Thirst;
    
    
    public int Stamina;
    
    public bool Hungry, Thirsty, IsDead;
    

    /// <summary>
    /// constructor without arguments sets default values
    /// </summary>
    public Living()
    {
        Damage = 0;
        Hunger = 10;
        Thirst = 20;
        Stamina = 10;
        IsDead = false;
    }

    public Living(int maxStamina, int hunger, int thirst)
    {
        Damage = 0;
        Stamina = maxStamina;
        Hunger = hunger;
        Thirst = thirst;
        IsDead = false;
    }
}