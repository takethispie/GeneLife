using Arch.Core;
using Arch.Core.Extensions;
using GeneLife.CommonComponents;
using GeneLife.Oracle.Components;

namespace GeneLife.Oracle.Services;

public static class RelationService
{
    public static bool CanBeTogether(Entity first, Entity second)
    {
        var related = false;
        if (first.Has<FamilyMember>() && second.Has<FamilyMember>())
            related = first.Get<FamilyMember>().FamilyId == second.Get<FamilyMember>().FamilyId;
        return new { fLiving = first.Has<Living>(), sLiving = second.Has<Living>(), related } switch
        {
            // human can't be in a relation with an inanimate object
            { fLiving: false } or { sLiving: false } => false, 
            // ( ͡° ͜ʖ ͡°)
            { related: true } => false,
            _ => false
        };
    }
}