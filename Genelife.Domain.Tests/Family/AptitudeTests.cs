using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Life;
using Genelife.Application.Systems;

namespace Genelife.UnitTests.Family;

public class AptitudeTests
{
    [Fact]
    public void StartingBonusMapsToExpectedSkillFamilies()
    {
        var science = new SkillProfile();
        AptitudeSkills.ApplyStartingBonus(ref science, Aptitude.Science);
        Assert.True(science.Medical > 0f);
        Assert.True(science.Engineering > 0f);
        Assert.Equal(0f, science.Art);

        var art = new SkillProfile();
        AptitudeSkills.ApplyStartingBonus(ref art, Aptitude.Art);
        Assert.True(art.Art > science.Art);

        var social = new SkillProfile();
        AptitudeSkills.ApplyStartingBonus(ref social, Aptitude.Social);
        Assert.True(social.Sales > 0f);

        var physical = new SkillProfile();
        AptitudeSkills.ApplyStartingBonus(ref physical, Aptitude.Physical);
        Assert.Equal(0f, physical.Total);
    }

    [Fact]
    public void LearningMultiplierOnlyMatchesRelevantFamilies()
    {
        Assert.True(AptitudeSkills.LearningMultiplier(Aptitude.Science, Skill.Medical) > 1f);
        Assert.True(AptitudeSkills.LearningMultiplier(Aptitude.Science, Skill.Engineering) > 1f);
        Assert.Equal(1f, AptitudeSkills.LearningMultiplier(Aptitude.Science, Skill.Art));
        Assert.Equal(1f, AptitudeSkills.LearningMultiplier(Aptitude.Physical, Skill.Programming));
    }

    [Fact]
    public void EducationGainsMoreForMatchingAptitude()
    {
        var world = World.Create();
        var economy = new Genelife.Economy.EconomyContext(world);
        var school = world.Create(new School("Arts", "Central", 30, 1f, Skill.Art));

        var science = CreateStudent(world, economy, "Science", Aptitude.Science, school);
        var artist = CreateStudent(world, economy, "Artist", Aptitude.Art, school);

        var education = new EducationSystem(world, economy);
        for (var month = 1; month <= 24; month++)
            education.Update(month * economy.Clock.TicksPerMonth);

        var scienceProfile = world.Get<SkillProfile>(science);
        var artistProfile = world.Get<SkillProfile>(artist);

        Assert.True(artistProfile.Art > scienceProfile.Art,
            $"art aptitude={artistProfile.Art}, science aptitude={scienceProfile.Art}");
        Assert.InRange(scienceProfile.Art, 0f, 1f);
        Assert.InRange(artistProfile.Art, 0f, 1f);
    }

    private static Entity CreateStudent(
        World world,
        Genelife.Economy.EconomyContext economy,
        string name,
        Aptitude aptitude,
        Entity school)
    {
        var entity = world.Create(
            new GenName(name),
            new Alive(10),
            new SkillProfile(),
            new Traits { Aptitude = aptitude },
            LifecycleFactory.Create(10, 0, economy.Clock, economy.Life.Policy),
            new Enrollment(school, 0, 1f));

        return entity;
    }
}
