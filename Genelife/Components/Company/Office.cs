using Arch.Core;
using Genelife.Values;

namespace Genelife.Components.Company;

/// <summary>
/// Component representing an office belonging to a company. Offices limit the
/// number of seats a company can reserve and carry a recurring operating cost.
/// </summary>
public record struct Office
{
    public string OfficeName;
    public Entity Company;
    public int Capacity;
    public Money OperatingCostPerCycle;
    public int SeatsUsed;
    public bool IsClosed;

    public Office(
        string officeName,
        Entity company,
        int capacity = 50,
        Money operatingCostPerCycle = default,
        int seatsUsed = 0,
        bool isClosed = false)
    {
        OfficeName = officeName;
        Company = company;
        Capacity = capacity;
        OperatingCostPerCycle = operatingCostPerCycle;
        SeatsUsed = seatsUsed;
        IsClosed = isClosed;
    }
}
