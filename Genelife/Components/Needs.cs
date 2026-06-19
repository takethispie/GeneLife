namespace Genelife.Components;

/// <summary>
/// Component representing a Sim's basic needs
/// </summary>
public record struct Needs
{
    public float Hunger;
    public float Energy;
    public float Hygiene;
    public float Bladder;

    public Needs()
    {
        Hunger = 100f;
        Energy = 100f;
        Hygiene = 100f;
        Bladder = 100f;
    }
}
