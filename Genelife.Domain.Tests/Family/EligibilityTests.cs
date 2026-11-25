using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Enums;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class EligibilityTests
{
    [Fact]
    public void SameSexPairingIsDisabledByDefault()
    {
        var context = new Context();
        var first = context.Genos(30, Sex.Female, 1);
        var second = context.Genos(28, Sex.Female, 2);

        var result = EligibilityPolicy.Evaluate(context.World, context.Index, first, second);

        Assert.False(result.Eligible);
        Assert.Equal("same-sex-not-allowed", result.Code);
    }

    [Fact]
    public void SameSexPairingCanBeEnabledByPolicy()
    {
        var context = new Context();
        var first = context.Genos(30, Sex.Female, 1);
        var second = context.Genos(28, Sex.Female, 2);
        var options = new RelationshipOptions { AllowSameSexPairing = true };

        var result = EligibilityPolicy.Evaluate(context.World, context.Index, first, second, options);

        Assert.True(result.Eligible);
    }

    [Theory]
    [InlineData(17, 30, "minor")]
    [InlineData(30, 30, null)]
    [InlineData(30, 71, "too-old")]
    [InlineData(30, 50, "age-gap")]
    public void AgeRulesReturnStableCodes(int ageA, int ageB, string? expectedCode)
    {
        var context = new Context();
        var first = context.Genos(ageA, Sex.Female, 1);
        var second = context.Genos(ageB, Sex.Male, 2);

        var result = EligibilityPolicy.Evaluate(context.World, context.Index, first, second);

        Assert.Equal(expectedCode is null, result.Eligible);
        Assert.Equal(expectedCode, result.Code);
    }

    [Fact]
    public void DependantsAndPartneredGenosesAreRejected()
    {
        var context = new Context();
        var dependent = context.Genos(19, Sex.Female, 1);
        var adult = context.Genos(21, Sex.Male, 2);
        var household = context.World.Create(new Household { Head = adult });
        context.World.Add(dependent, new HouseholdMember(household, HouseholdRole.Child));

        Assert.Equal("dependent",
            EligibilityPolicy.Evaluate(context.World, context.Index, dependent, adult).Code);

        var first = context.Genos(30, Sex.Female, 3);
        var second = context.Genos(32, Sex.Male, 4);
        var third = context.Genos(31, Sex.Male, 5);
        context.Service.CreatePair(first, second, RelationshipKind.Partner, RelationshipSource.Seed, 1);

        Assert.Equal("has-partner",
            EligibilityPolicy.Evaluate(context.World, context.Index, first, third).Code);
    }

    [Fact]
    public void CloseRelativesAreRejected()
    {
        var context = new Context();
        var parent = context.Genos(30, Sex.Male, 1);
        var child = context.Genos(20, Sex.Female, 2);
        context.Service.CreatePair(parent, child, RelationshipKind.Parent, RelationshipSource.Seed, 1);

        Assert.Equal("close-relative",
            EligibilityPolicy.Evaluate(context.World, context.Index, parent, child).Code);

        var sibling = context.Genos(22, Sex.Male, 3);
        context.Service.CreatePair(parent, sibling, RelationshipKind.Parent, RelationshipSource.Seed, 1);
        Assert.Equal("close-relative",
            EligibilityPolicy.Evaluate(context.World, context.Index, child, sibling).Code);
    }

    [Fact]
    public void FirstCousinsAreRejected()
    {
        var context = new Context();
        var grandparentA = context.Genos(70, Sex.Male, 1);
        var grandparentB = context.Genos(68, Sex.Female, 2);
        var parentA = context.Genos(45, Sex.Female, 3);
        var parentB = context.Genos(43, Sex.Male, 4);
        var cousinA = context.Genos(21, Sex.Female, 5);
        var cousinB = context.Genos(22, Sex.Male, 6);

        context.Service.CreatePair(grandparentA, parentA, RelationshipKind.Parent, RelationshipSource.Seed, 1);
        context.Service.CreatePair(grandparentB, parentA, RelationshipKind.Parent, RelationshipSource.Seed, 1);
        context.Service.CreatePair(grandparentA, parentB, RelationshipKind.Parent, RelationshipSource.Seed, 1);
        context.Service.CreatePair(grandparentB, parentB, RelationshipKind.Parent, RelationshipSource.Seed, 1);
        context.Service.CreatePair(parentA, cousinA, RelationshipKind.Parent, RelationshipSource.Seed, 1);
        context.Service.CreatePair(parentB, cousinB, RelationshipKind.Parent, RelationshipSource.Seed, 1);

        Assert.Equal("close-relative",
            EligibilityPolicy.Evaluate(context.World, context.Index, cousinA, cousinB).Code);
    }

    [Fact]
    public void CooldownCallbackRejectsThePair()
    {
        var context = new Context();
        var first = context.Genos(30, Sex.Female, 1);
        var second = context.Genos(31, Sex.Male, 2);

        var result = EligibilityPolicy.Evaluate(
            context.World, context.Index, first, second,
            inCooldown: (_, _) => true);

        Assert.False(result.Eligible);
        Assert.Equal("cooldown", result.Code);
    }

    private sealed class Context
    {
        public World World { get; } = World.Create();
        public RelationshipIndex Index { get; } = new();
        public RelationshipService Service { get; }

        public Context()
        {
            Service = new RelationshipService(World, Index);
        }

        public Entity Genos(int age, Sex sex, ulong networkId) =>
            World.Create(
                new GenName($"Genos {networkId}"),
                new Alive(age),
                new Traits { Sex = sex },
                new NetworkId(networkId));
    }
}
