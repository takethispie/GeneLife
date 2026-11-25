namespace Genelife.Components.Gen;

/// <summary>
/// Component for identifying a Genos
/// </summary>
public record struct GenName
{
    public string Name;
    
    public GenName(string name)
    {
        Name = name;
    }
}
