using Arch.Core;
using GeneLife.Common.Components;
using GeneLife.Common.Components.Utils;

namespace GeneLife.Utils;

public static class IdentityExtensions
{
    public static string FullName(this Identity id)
    {
        return $"{id.FirstName} {id.LastName}";
    }
}