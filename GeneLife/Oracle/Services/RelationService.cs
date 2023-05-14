using Arch.Core;
using Arch.Core.Extensions;
using GeneLife.CommonComponents;
using GeneLife.Genetic;
using GeneLife.Oracle.Components;
using GeneLife.Oracle.Core;
using GeneLife.Utils;

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
            { fLiving: true, sLiving: true, related: false } => true,
            _ => false
        };
    }

    public static float ComputeAttractivenessChances(Genome first, Genome second)
    {
        //Load sample data
        var sampleData = new Attractiveness.ModelInput()
        {
            F_EyeColor = first.EyeColor.ToString(),
            F_HairType = first.HairColor.ToString(),
            F_Morphotype = first.Morphotype.ToString(),
            F_Intelligence = first.Intelligence.ToString(),
            F_HeightPotential = first.HeightPotential.ToString(),
            F_Age = first.Age,
            F_Sex = first.Sex.ToString(),
            F_BehaviorPropension = first.BehaviorPropension.ToString(),
            S_EyeColor = second.EyeColor.ToString(),
            S_HairType = second.HairColor.ToString(),
            S_Morphotype = second.Morphotype.ToString(),
            S_Intelligence = second.Intelligence.ToString(),
            S_HeightPotential = second.HeightPotential.ToString(),
            S_Age = second.Age,
            S_Sex = second.Sex.ToString(),
            S_BehaviorPropension = second.BehaviorPropension.ToString(),
        };

        //Load model and predict output
        var result = Attractiveness.Predict(sampleData);
        return result.Score[1];
    }

    public static bool EndsUpTogether(float chances) => new Random().NextSingle() <= chances;

    public static void LoveLoop(List<Entity> entities)
    {
        entities.ForEach(entity =>
        {
            if (entity.Has<Relation>()) return;
            var pos = entity.Get<Position>();
            var possibleMatches = pos.Coordinates.GetEntitiesInsideCube(20, entities.ToArray())
                .Where(x => x.Id != entity.Id);
            foreach (var pMatch in possibleMatches)
            {
                if(!CanBeTogether(entity, pMatch)) continue;
                var result = ComputeAttractivenessChances(entity.Get<Genome>(), pMatch.Get<Genome>());
                if (EndsUpTogether(result))
                {
                    entity.Set(new Relation(pMatch.Id));
                    pMatch.Set(new Relation(entity.Id));
                };
            }
        });
    }
}