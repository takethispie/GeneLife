using Genelife.Api.DTOs;

namespace Genelife.Api.Models;

public class GenosDocument
{
    public Guid Id { get; set; }
    public long NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public float Hunger { get; set; }
    public float Energy { get; set; }
    public float Hygiene { get; set; }
    public float Bladder { get; set; }
    public string CurrentAction { get; set; } = string.Empty;
    public float ActionTimeRemaining { get; set; }
    public bool IsPlayerControlled { get; set; }
    public string? PlayerId { get; set; }
    public string Stage { get; set; } = string.Empty;
    public float Health { get; set; } = 100f;
    public float SkillTotal { get; set; }
    public string Seniority { get; set; } = string.Empty;
    public byte? SkinTone { get; set; }
    public byte? FaceShape { get; set; }
    public byte? HairColor { get; set; }
    public byte? HairStyle { get; set; }
    public byte? EyeColor { get; set; }
    public byte? BodyFrame { get; set; }
    public byte? HeightScale { get; set; }
    public byte? AgeBand { get; set; }
    public byte? Expression { get; set; }
    public byte? AppearanceVersion { get; set; }
    public long HouseholdNetworkId { get; set; }
    public long HousingUnitNetworkId { get; set; }
    public string Tenure { get; set; } = string.Empty;
    public string HousingTier { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public GenosStatus ToGenosStatus()
    {
        return new GenosStatus
        {
            NetworkId = (ulong)NetworkId,
            Name = Name,
            Age = Age,
            IsPlayerControlled = IsPlayerControlled,
            Hunger = MathF.Round(Hunger, 1),
            Energy = MathF.Round(Energy, 1),
            Hygiene = MathF.Round(Hygiene, 1),
            Bladder = MathF.Round(Bladder, 1),
            CurrentAction = CurrentAction,
            TimeRemaining = MathF.Round(ActionTimeRemaining, 2),
            HouseholdNetworkId = HouseholdNetworkId == 0 ? null : (ulong)HouseholdNetworkId,
            HousingUnitNetworkId = HousingUnitNetworkId == 0 ? null : (ulong)HousingUnitNetworkId,
            Tenure = Tenure,
            HousingTier = HousingTier,
            Homeless = HousingUnitNetworkId == 0,
            Stage = Stage,
            Health = (decimal)MathF.Round(Health, 1),
            TopSkill = string.Empty,
            Seniority = Seniority,
            Appearance = SkinTone is null ? null : new AppearanceDto
            {
                SkinTone = SkinTone.Value,
                FaceShape = FaceShape ?? 0,
                HairColor = HairColor ?? 0,
                HairStyle = HairStyle ?? 0,
                EyeColor = EyeColor ?? 0,
                BodyFrame = BodyFrame ?? 0,
                HeightScale = HeightScale ?? 0,
                AgeBand = AgeBand ?? 0,
                Expression = Expression ?? 0,
                AppearanceVersion = AppearanceVersion ?? 1
            }
        };
    }
}