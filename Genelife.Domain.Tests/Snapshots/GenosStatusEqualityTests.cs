using System.Reflection;
using Genelife.Api.DTOs;
using Genelife.Api.Snapshots;

namespace Genelife.UnitTests.Snapshots;

/// <summary>
/// Guards the delta comparison against silent omissions. A field missing from
/// <see cref="WorldSnapshotDelta.GenosesEqual"/> is never replicated to clients,
/// and that failure mode is invisible — so every property is probed by reflection
/// rather than trusting a hand-maintained list.
/// </summary>
public class GenosStatusEqualityTests
{
    [Fact]
    public void IdenticalCopies_CompareEqual()
    {
        var baseline = Baseline();
        var copy = Clone(baseline);

        Assert.True(WorldSnapshotDelta.GenosesEqual(baseline, copy));
    }

    [Fact]
    public void EveryProperty_ParticipatesInEquality()
    {
        var baseline = Baseline();
        var ignored = new List<string>();

        foreach (var property in typeof(GenosStatus).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var variant = Clone(baseline);
            var current = property.GetValue(variant);
            property.SetValue(variant, Perturb(current, property.PropertyType));

            if (WorldSnapshotDelta.GenosesEqual(baseline, variant))
                ignored.Add(property.Name);
        }

        Assert.True(
            ignored.Count == 0,
            $"GenosesEqual ignores these fields, so changes to them produce no delta: {string.Join(", ", ignored)}");
    }

    private static GenosStatus Baseline() => new()
    {
        NetworkId = 7,
        Name = "Ada",
        Age = 33,
        IsPlayerControlled = false,
        Hunger = 61.5f,
        Energy = 42f,
        Hygiene = 77f,
        Bladder = 88f,
        CurrentAction = "Working",
        TimeRemaining = 1.5f,
        X = 10.5f,
        Y = 20.5f,
        TileX = 3,
        TileY = 4,
        HasMoveTarget = true,
        MoveTargetX = 5,
        MoveTargetY = 6,
        WalletBalance = 123.45m,
        SavingsBalance = 678.9m,
        UnmetDemand = 12.3m,
        HouseholdNetworkId = 11,
        HousingUnitNetworkId = 12,
        Tenure = "Owner",
        HousingTier = "Mid",
        Homeless = false,
        Stage = "Work",
        Health = 91.5m,
        TopSkill = "Engineering",
        Seniority = "Associate",
        IsDead = false,
        Appearance = new AppearanceDto
        {
            SkinTone = 1, FaceShape = 2, HairColor = 3, HairStyle = 4, EyeColor = 5,
            BodyFrame = 6, HeightScale = 7, AgeBand = 8, Expression = 9, AppearanceVersion = 1
        }
    };

    private static GenosStatus Clone(GenosStatus source)
    {
        var clone = new GenosStatus();
        foreach (var property in typeof(GenosStatus).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            property.SetValue(clone, property.GetValue(source));
        return clone;
    }

    private static object? Perturb(object? value, Type type)
    {
        if (type == typeof(string)) return (string?)value + "x";
        if (type == typeof(int)) return (int)value! + 1;
        if (type == typeof(float)) return (float)value! + 1f;
        if (type == typeof(decimal)) return (decimal)value! + 1m;
        if (type == typeof(bool)) return !(bool)value!;
        if (type == typeof(ulong)) return (ulong)value! + 1UL;
        if (type == typeof(ulong?)) return value is ulong id ? id + 1UL : 1UL;
        if (type == typeof(AppearanceDto)) return new AppearanceDto { SkinTone = 99, AppearanceVersion = 2 };
        throw new InvalidOperationException($"No perturbation for {type.Name}");
    }
}