namespace Genelife.Enums;

/// <summary>
/// How a household occupies its unit. Dependents are members of an adult
/// household and are never charged directly.
/// </summary>
public enum Tenure
{
    Dependent,
    Renter,
    Owner,
    Sheltered
}
