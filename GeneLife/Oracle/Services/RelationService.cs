using System.Collections.Concurrent;
using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using GeneLife.CommonComponents;
using GeneLife.CommonComponents.Utils;
using GeneLife.Data;
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
        return new
            {
                fLiving = first.Has<Living>(),
                sLiving = second.Has<Living>(),
                related,
                fAge = first.Get<Genome>().Age,
                sAge = second.Get<Genome>().Age,
                fRelationAlready = first.Has<Relation>(),
                sRelationAlready = second.Has<Relation>()
            } switch
            {
                { fLiving: false } or { sLiving: false }
                    or { fAge: < 18, sAge: > 18 }
                    or { fAge: > 18, sAge: < 18 }
                    or { fRelationAlready: true }
                    or { sRelationAlready: true }
                    // ( ͡° ͜ʖ ͡°)
                    or { related: true } => false,
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
        var matchLists = new List<(Entity entity, IEnumerable<Entity> matches)>();
        //Parallel.ForEach(entities, entity => { });
        Parallel.ForEach(entities, entity =>
        {
            if (entity.Has<Relation>()) return;
            var pos = entity.Get<Position>();

            var possibleMatches = pos.Coordinates
                .GetEntitiesInsideCube(20, entities.ToArray())
                .Where(x => x.Id != entity.Id && CanBeTogether(entity, x))
                .ToArray();
            
            var selected = possibleMatches.Take(new Random().Next(0, Constants.MaxAttractionComputePerLoveInterestLoop)).ToArray();
            matchLists.Add((entity, selected));
        });

        matchLists.ForEach(matchList =>
        {
            foreach (var match in matchList.matches)
            {
                var result = ComputeAttractivenessChances(matchList.entity.Get<Genome>(), match.Get<Genome>());
                if (!EndsUpTogether(result)) continue;
                var first = matchList.entity.Get<Identity>();
                var second = match.Get<Identity>();
                EventBus.Send(new LogEvent { Message = $"{first.FirstName} {first.LastName} and {second.FirstName} {second.LastName} are now together !"});
                matchList.entity.Add(new Relation(match.Id));
                match.Add(new Relation(matchList.entity.Id));
            }
        });
    }
}