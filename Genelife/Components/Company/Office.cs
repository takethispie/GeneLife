using Arch.Core;

namespace Genelife.Components.Company;

/// <summary>
/// Component representing an office belonging to a company
/// </summary>
public record struct Office
{
    public string OfficeName;
    public Entity Company;
    public int Capacity;

    public Office(string officeName, Entity company, int capacity = 50)
    {
        OfficeName = officeName;
        Company = company;
        Capacity = capacity;
    }
}
