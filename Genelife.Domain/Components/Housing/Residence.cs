using Arch.Core;

namespace Genelife.Components.Housing;

/// <summary>
/// Derived genos to home cache, maintained by the housing registry in apply
/// phases. Genoses without a residence are homeless.
/// </summary>
public record struct Residence
{
    public Entity Household;
    public Entity Unit;

    public Residence(Entity household, Entity unit)
    {
        Household = household;
        Unit = unit;
    }
}
