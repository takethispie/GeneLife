namespace Genelife.Components;

/// <summary>
/// Component representing an office belonging to a company
/// </summary>
public record struct Office
{
    public string OfficeName;
    public int CompanyEntityId;
    public int Capacity;
    
    public Office(string officeName, int companyEntityId, int capacity = 50)
    {
        OfficeName = officeName;
        CompanyEntityId = companyEntityId;
        Capacity = capacity;
    }
}
