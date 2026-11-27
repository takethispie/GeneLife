using Genelife.Api.DTOs;
using Genelife.Api.Snapshots;

namespace Genelife.UnitTests.Snapshots;

/// <summary>
/// Guards the genos delta comparison against silent omissions. A field missing from
/// <see cref="WorldSnapshotDelta.GenosesEqual"/> is never replicated to clients, and that
/// failure mode is invisible — so every property is probed by reflection rather than trusting a
/// hand-maintained list. The shared probe lives in <see cref="DtoGuard"/>.
/// </summary>
public class GenosStatusEqualityTests
{
    [Fact]
    public void IdenticalCopies_CompareEqual()
    {
        var baseline = Baseline();

        Assert.True(WorldSnapshotDelta.GenosesEqual(baseline, DtoGuard.Clone(baseline)));
    }

    [Fact]
    public void EveryProperty_ParticipatesInEquality()
    {
        var ignored = DtoGuard.IgnoredProperties(Baseline(), WorldSnapshotDelta.GenosesEqual);

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
        HousingBuildingNetworkId = 13,
        WorkplaceBuildingNetworkId = 14,
        HousingUnitNumber = "12B",
        JobPosition = "Engineer",
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
}