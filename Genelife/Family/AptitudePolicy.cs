using Genelife.Components.Gen;

namespace Genelife.Family;

/// <summary>
/// Aptitude biases starting skills and learning for the matching skill family.
/// It never replaces <see cref="Genelife.Components.Life.SkillProfile"/>.
/// </summary>
public sealed class AptitudePolicy
{
    public decimal ScienceBonus { get; set; } = 0.10m;
    public decimal ArtBonus { get; set; } = 0.15m;
    public decimal CraftBonus { get; set; } = 0.10m;
    public decimal SocialBonus { get; set; } = 0.15m;
    public decimal LearningMultiplier { get; set; } = 1.15m;

    public static AptitudePolicy Default { get; } = new();

    public void Validate()
    {
        if (ScienceBonus < 0m || ArtBonus < 0m || CraftBonus < 0m || SocialBonus < 0m)
            throw new ArgumentOutOfRangeException(nameof(ScienceBonus), "Aptitude bonuses must be non-negative.");

        if (LearningMultiplier < 1m)
            throw new ArgumentOutOfRangeException(
                nameof(LearningMultiplier), "LearningMultiplier must be at least 1.");
    }
}
