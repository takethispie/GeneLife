using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;

namespace GeneLife.Core.Utils;

public static class IdentityExtensions
{
    public static string FullName(this Identity id)
    {
        return $"{id.FirstName} {id.LastName}";
    }
}