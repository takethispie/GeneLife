using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Enums;

namespace Genelife.Family;

public static class AptitudeSkills
{
    public static void ApplyStartingBonus(
        ref SkillProfile profile,
        Aptitude aptitude,
        AptitudePolicy? policy = null)
    {
        policy ??= AptitudePolicy.Default;
        policy.Validate();

        foreach (var skill in SkillsFor(aptitude))
            profile.Add(skill, (float)BonusFor(aptitude, policy));
    }

    public static float LearningMultiplier(
        Aptitude aptitude,
        Skill skill,
        AptitudePolicy? policy = null)
    {
        policy ??= AptitudePolicy.Default;
        policy.Validate();

        return IsMatch(aptitude, skill) ? (float)policy.LearningMultiplier : 1f;
    }

    public static IReadOnlyList<Skill> SkillsFor(Aptitude aptitude) => aptitude switch
    {
        Aptitude.Science => [Skill.Medical, Skill.Engineering],
        Aptitude.Art => [Skill.Art],
        Aptitude.Craft => [Skill.Cooking, Skill.Engineering],
        Aptitude.Social => [Skill.Sales],
        Aptitude.Physical => [],
        _ => []
    };

    public static bool IsMatch(Aptitude aptitude, Skill skill) =>
        SkillsFor(aptitude).Any(match => skill.HasFlag(match));

    private static decimal BonusFor(Aptitude aptitude, AptitudePolicy policy) => aptitude switch
    {
        Aptitude.Science => policy.ScienceBonus,
        Aptitude.Art => policy.ArtBonus,
        Aptitude.Craft => policy.CraftBonus,
        Aptitude.Social => policy.SocialBonus,
        _ => 0m
    };
}
