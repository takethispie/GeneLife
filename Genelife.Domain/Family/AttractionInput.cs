using Genelife.Components.Gen;

namespace Genelife.Family;

public readonly record struct AttractionInput(
    ulong SimNetworkIdA,
    ulong SimNetworkIdB,
    Traits TraitsA,
    Traits TraitsB,
    int AgeA,
    int AgeB,
    bool SameHousehold,
    bool SameWorkplace,
    bool SameSchool,
    int TileDistance,
    decimal IncomeA,
    decimal IncomeB,
    bool EmployedA,
    bool EmployedB,
    bool HousedA,
    bool HousedB,
    int HealthA,
    int HealthB,
    bool AvailableA,
    bool AvailableB);

public readonly record struct AttractionComponent(string Name, int Points, int Max);

public sealed record AttractionResult(
    int Score,
    IReadOnlyList<AttractionComponent> Components,
    bool Eligible,
    string? RejectionReason);

public static partial class AttractionScorer
{
    public const string TraitCompatibilityName = "trait-compatibility";
    public const string AgePreferenceName = "age-preference";
    public const string ProximityName = "proximity";
    public const string StabilityName = "stability";
    public const string HealthName = "health";
    public const string AvailabilityName = "availability";

    public static AttractionResult Score(
        in AttractionInput input,
        in EligibilityResult eligibility,
        RelationshipOptions? options = null,
        AttractionCoefficients? coefficients = null)
    {
        options ??= RelationshipOptions.Default;
        coefficients ??= AttractionCoefficients.Default;
        coefficients.Validate();

        if (!eligibility.Eligible)
            return new AttractionResult(0, Array.Empty<AttractionComponent>(), false, eligibility.Reason);

        var traitBp = TraitCompatibilityBasisPoints(input.TraitsA, input.TraitsB);
        var ageBp = AgeBasisPoints(input.AgeA, input.AgeB, options.MaxAgeGap);
        var proximityBp = ProximityBasisPoints(input);
        var stabilityBp = StabilityBasisPoints(input);
        var healthBp = HealthBasisPoints(input.HealthA, input.HealthB);
        var availabilityBp = AvailabilityBasisPoints(input.AvailableA, input.AvailableB);

        var components = new[]
        {
            new AttractionComponent(TraitCompatibilityName, ScaleComponent(traitBp, coefficients.TraitCompatibility), coefficients.TraitCompatibility),
            new AttractionComponent(AgePreferenceName, ScaleComponent(ageBp, coefficients.AgePreference), coefficients.AgePreference),
            new AttractionComponent(ProximityName, ScaleComponent(proximityBp, coefficients.Proximity), coefficients.Proximity),
            new AttractionComponent(StabilityName, ScaleComponent(stabilityBp, coefficients.Stability), coefficients.Stability),
            new AttractionComponent(HealthName, ScaleComponent(healthBp, coefficients.Health), coefficients.Health),
            new AttractionComponent(AvailabilityName, ScaleComponent(availabilityBp, coefficients.Availability), coefficients.Availability)
        };

        var total = Math.Clamp(components.Sum(component => component.Points), 0, ScoreScale);
        return new AttractionResult(total, components, true, null);
    }

    public static int TraitCompatibilityBasisPoints(in Traits first, in Traits second)
    {
        long weighted = 0;
        long total = 0;

        Add(100, DistanceBp(first.EyeColor, second.EyeColor));
        Add(100, DistanceBp(first.HairColor, second.HairColor));
        Add(50, DistanceBp(first.Handedness, second.Handedness));
        Add(200, DistanceBp(first.Morphotype, second.Morphotype));
        Add(300, DistanceBp(first.Aptitude, second.Aptitude));
        Add(150, DistanceBp(first.HeightPotential, second.HeightPotential));
        Add(250, DistanceBp(first.Temperament, second.Temperament));

        return total == 0 ? 1000 : (int)(weighted / total);

        void Add(int weight, int distanceBp)
        {
            weighted += (long)weight * (1000 - distanceBp);
            total += weight;
        }
    }

    public static int DistanceBp<T>(T first, T second)
        where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        if (values.Length <= 1)
            return 0;
        var firstIndex = Array.IndexOf(values, first);
        var secondIndex = Array.IndexOf(values, second);
        if (firstIndex < 0 || secondIndex < 0)
            return 1000;
        return Math.Abs(firstIndex - secondIndex) * 1000 / (values.Length - 1);
    }

    private static int AgeBasisPoints(int firstAge, int secondAge, int maxAgeGap)
    {
        if (maxAgeGap <= 0)
            return 1000;
        var gap = Math.Abs(firstAge - secondAge);
        return Math.Clamp(1000 - gap * 1000 / maxAgeGap, 0, 1000);
    }

    private static int ProximityBasisPoints(in AttractionInput input)
    {
        if (input.SameHousehold)
            return 1000;
        if (input.SameWorkplace || input.SameSchool)
            return 750;
        if (input.TileDistance <= 5)
            return 500;
        if (input.TileDistance <= 15)
            return 300;
        if (input.TileDistance <= 30)
            return 150;
        return 0;
    }

    private static int StabilityBasisPoints(in AttractionInput input)
    {
        var first = Stability(input.EmployedA, input.HousedA, input.IncomeA);
        var second = Stability(input.EmployedB, input.HousedB, input.IncomeB);
        return (first + second) / 2;
    }

    private static int Stability(bool employed, bool housed, decimal income)
    {
        var incomeBand = Math.Clamp(income / 2_000m, 0m, 1m);
        return (employed ? 400 : 0) + (housed ? 300 : 0) + (int)(incomeBand * 300m);
    }

    private static int HealthBasisPoints(int firstHealth, int secondHealth)
    {
        static int Clamp(int value) => Math.Clamp(value, 0, 100);
        return (Clamp(firstHealth) + Clamp(secondHealth)) * 1000 / 200;
    }

    private static int AvailabilityBasisPoints(bool first, bool second) =>
        first && second ? 1000 : first || second ? 500 : 0;

    private static int ScaleComponent(int basisPoints, int componentMax) =>
        (int)(((long)Math.Clamp(basisPoints, 0, 1000) * componentMax + 500) / 1000);
}
