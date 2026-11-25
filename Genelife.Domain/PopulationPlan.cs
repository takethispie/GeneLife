namespace Genelife;

/// <summary>
/// Population generation input shared by housing assignment and life stages.
/// The plan is intentionally small and deterministic: the same plan and seed
/// always produce the same households and units.
/// </summary>
public sealed class PopulationPlan
{
    public int Adults { get; set; }
    public int Children { get; set; }
    public decimal ExpectedMonthlyIncome { get; set; } = 300m;
    public int ShelterBeds { get; set; }
    public int BasicUnits { get; set; }
    public int ComfortableUnits { get; set; }
    public int LuxuryUnits { get; set; }

    public static PopulationPlan Default { get; } = new()
    {
        Adults = 40,
        Children = 20,
        ExpectedMonthlyIncome = 300m,
        ShelterBeds = 4,
        BasicUnits = 30,
        ComfortableUnits = 10,
        LuxuryUnits = 2
    };

    public void Validate()
    {
        if (Adults < 0 || Children < 0)
            throw new ArgumentOutOfRangeException(nameof(Adults), "Population counts cannot be negative.");
        if (Adults == 0 && Children > 0)
            throw new InvalidOperationException("Children require at least one adult.");
    }
}
