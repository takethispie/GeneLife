namespace Genelife.Components;

/// <summary>
/// Component for identifying a Sim
/// </summary>
public record struct SimName
{
    public string Name;
    
    public SimName(string name)
    {
        Name = name;
    }
}
