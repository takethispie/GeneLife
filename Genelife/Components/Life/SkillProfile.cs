using Genelife.Enums;

namespace Genelife.Components.Life;

/// <summary>
/// Proficiency per skill family in [0, 1]. The <see cref="Skill"/> bitmask used by
/// hot employment components is derived from this profile: a family is present
/// when proficiency is at least <see cref="MaskThreshold"/>.
/// </summary>
public record struct SkillProfile
{
    public const float MaskThreshold = 0.25f;

    public static readonly Skill[] Families =
    [
        Skill.Programming, Skill.Art, Skill.Cooking,
        Skill.Medical, Skill.Engineering, Skill.Sales
    ];

    public float Programming;
    public float Art;
    public float Cooking;
    public float Medical;
    public float Engineering;
    public float Sales;

    public float Total => Programming + Art + Cooking + Medical + Engineering + Sales;

    public float Get(Skill skill) => skill switch
    {
        Skill.Programming => Programming,
        Skill.Art => Art,
        Skill.Cooking => Cooking,
        Skill.Medical => Medical,
        Skill.Engineering => Engineering,
        Skill.Sales => Sales,
        _ => 0f
    };

    public void Add(Skill skill, float amount)
    {
        if (amount <= 0f)
            return;

        if (skill.HasFlag(Skill.Programming)) Programming = Cap(Programming + amount);
        if (skill.HasFlag(Skill.Art)) Art = Cap(Art + amount);
        if (skill.HasFlag(Skill.Cooking)) Cooking = Cap(Cooking + amount);
        if (skill.HasFlag(Skill.Medical)) Medical = Cap(Medical + amount);
        if (skill.HasFlag(Skill.Engineering)) Engineering = Cap(Engineering + amount);
        if (skill.HasFlag(Skill.Sales)) Sales = Cap(Sales + amount);
    }

    public Skill ToMask()
    {
        var mask = Skill.None;
        if (Programming >= MaskThreshold) mask |= Skill.Programming;
        if (Art >= MaskThreshold) mask |= Skill.Art;
        if (Cooking >= MaskThreshold) mask |= Skill.Cooking;
        if (Medical >= MaskThreshold) mask |= Skill.Medical;
        if (Engineering >= MaskThreshold) mask |= Skill.Engineering;
        if (Sales >= MaskThreshold) mask |= Skill.Sales;
        return mask;
    }

    public static SkillProfile FromMask(Skill mask, float value = 0.5f)
    {
        var profile = new SkillProfile();
        profile.Add(mask, value);
        return profile;
    }


    private static float Cap(float value) => Math.Clamp(value, 0f, 1f);
}
