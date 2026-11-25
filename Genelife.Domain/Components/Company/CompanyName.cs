namespace Genelife.Components.Company;

/// <summary>
/// Component for identifying a Company
/// </summary>
public record struct CompanyName
{
    public string Name;
    
    public CompanyName(string name)
    {
        Name = name;
    }
}
