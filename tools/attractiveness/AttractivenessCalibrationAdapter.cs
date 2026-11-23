using AttractivenessService;
using Genelife.Components.Gen;

namespace AttractivenessCalibration;

/// <summary>
/// The only place that knows the ML.NET prototype's column names. It maps the
/// canonical <see cref="Traits"/> and <see cref="Alive.Age"/> onto the prototype
/// input row. The runtime never references this adapter or Microsoft.ML.
/// </summary>
public static class AttractivenessCalibrationAdapter
{
    public static Attractiveness.ModelInput Map(
        in Traits first,
        int firstAge,
        in Traits second,
        int secondAge)
    {
        return new Attractiveness.ModelInput
        {
            F_EyeColor = EyeColorName(first.EyeColor),
            F_HairType = HairTypeName(first.HairColor),
            F_Handedness = HandednessName(first.Handedness),
            F_Morphotype = MorphotypeName(first.Morphotype),
            F_Intelligence = IntelligenceName(first.Aptitude),
            F_HeightPotential = HeightPotentialName(first.HeightPotential),
            F_Age = firstAge,
            F_Sex = SexName(first.Sex),
            F_BehaviorPropension = BehaviorPropensionName(first.Temperament),
            S_EyeColor = EyeColorName(second.EyeColor),
            S_HairType = HairTypeName(second.HairColor),
            S_Handedness = HandednessName(second.Handedness),
            S_Morphotype = MorphotypeName(second.Morphotype),
            S_Intelligence = IntelligenceName(second.Aptitude),
            S_HeightPotential = HeightPotentialName(second.HeightPotential),
            S_Age = secondAge,
            S_Sex = SexName(second.Sex),
            S_BehaviorPropension = BehaviorPropensionName(second.Temperament),
            Result = 0
        };
    }

    public static string EyeColorName(EyeColor value) => value switch
    {
        EyeColor.Brown => "Brown",
        EyeColor.Blue => "Blue",
        EyeColor.Green => "Green",
        EyeColor.Grey => "Grey",
        EyeColor.Hazel => "Hazel",
        _ => value.ToString()
    };

    public static string HairTypeName(HairColor value) => value switch
    {
        HairColor.Black => "Black",
        HairColor.Brown => "Brown",
        HairColor.Blonde => "Blonde",
        HairColor.Ginger => "Ginger",
        HairColor.Grey => "Grey",
        _ => value.ToString()
    };

    public static string HandednessName(Handedness value) => value switch
    {
        Handedness.Right => "RightHanded",
        Handedness.Left => "LeftHanded",
        Handedness.Ambidextrous => "Ambidextrous",
        _ => value.ToString()
    };

    public static string MorphotypeName(Morphotype value) => value switch
    {
        Morphotype.Ectomorph => "Ectomorph",
        Morphotype.Mesomorph => "Mesomorph",
        Morphotype.Endomorph => "Endomorph",
        _ => value.ToString()
    };

    public static string IntelligenceName(Aptitude value) => value switch
    {
        Aptitude.Science => "Science",
        Aptitude.Art => "Art",
        Aptitude.Craft => "Craft",
        Aptitude.Physical => "Physical",
        _ => value.ToString()
    };

    public static string HeightPotentialName(HeightPotential value) => value switch
    {
        HeightPotential.Short => "Short",
        HeightPotential.Average => "Average",
        HeightPotential.Tall => "Tall",
        _ => value.ToString()
    };

    public static string SexName(Sex value) => value switch
    {
        Sex.Female => "Female",
        Sex.Male => "Male",
        _ => value.ToString()
    };

    public static string BehaviorPropensionName(Temperament value) => value switch
    {
        Temperament.Peaceful => "Peaceful",
        Temperament.Neutral => "Emotional",
        Temperament.Violent => "Violent",
        _ => value.ToString()
    };
}
