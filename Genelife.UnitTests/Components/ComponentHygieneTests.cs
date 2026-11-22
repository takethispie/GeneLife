using System.Reflection;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Enums;

namespace Genelife.UnitTests.Components;

public class ComponentHygieneTests
{
    [Fact]
    public void HotEmploymentComponents_ContainNoListFields()
    {
        AssertNoListFields(typeof(JobPosting));
        AssertNoListFields(typeof(JobSeeker));
    }

    [Fact]
    public void HotHousingComponents_ContainNoListFields()
    {
        AssertNoListFields(typeof(Property));
        AssertNoListFields(typeof(HousingUnit));
        AssertNoListFields(typeof(Household));
        AssertNoListFields(typeof(HouseholdMember));
        AssertNoListFields(typeof(Residence));
        AssertNoListFields(typeof(ConstructionProject));
        AssertNoListFields(typeof(NeedsModifier));
    }

    [Fact]
    public void JobSeeker_SkillsUseBitmaskTokens()
    {
        var seeker = new JobSeeker(skills: Skill.Programming | Skill.Medical);

        Assert.True(seeker.Skills.HasFlag(Skill.Programming));
        Assert.True(seeker.Skills.HasFlag(Skill.Medical));
        Assert.False(seeker.Skills.HasFlag(Skill.Art));
    }

    private static void AssertNoListFields(Type type)
    {
        var listFields = type
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(field => field.FieldType.IsGenericType
                && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
            .Select(field => field.Name)
            .ToArray();

        Assert.True(listFields.Length == 0,
            $"{type.Name} still has List<T> fields: {string.Join(", ", listFields)}");
    }
}
