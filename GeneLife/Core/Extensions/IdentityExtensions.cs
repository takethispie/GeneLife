using GeneLife.Core.Components.Characters;

namespace GeneLife.Core.Extensions;

public static class IdentityExtensions
{
    public static string FullName(this Identity id)
    {
        return $"{id.FirstName} {id.LastName}";
    }
}