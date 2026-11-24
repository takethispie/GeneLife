using System.Reflection;
using System.Runtime.InteropServices;
using Genelife.Components.Gen;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class TraitDomainTests
{
    [Fact]
    public void Traits_IsTenBytesAndContainsNoAgeField()
    {
        Assert.Equal(10, Marshal.SizeOf<Traits>());
        Assert.DoesNotContain(
            typeof(Traits).GetFields(BindingFlags.Public | BindingFlags.Instance),
            field => field.Name.Equals("Age", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EveryEnumValueRoundTripsThroughItsNumericValue()
    {
        AssertRoundTrip<EyeColor>();
        AssertRoundTrip<HairColor>();
        AssertRoundTrip<Handedness>();
        AssertRoundTrip<Morphotype>();
        AssertRoundTrip<Aptitude>();
        AssertRoundTrip<HeightPotential>();
        AssertRoundTrip<Sex>();
        AssertRoundTrip<Temperament>();
        AssertRoundTrip<SkinTone>();
        AssertRoundTrip<FaceShape>();
    }

    [Fact]
    public void ValidationAcceptsEveryDefinedCombination()
    {
        foreach (var eye in Enum.GetValues<EyeColor>())
        {
            var traits = new Traits { EyeColor = eye };
            Assert.True(TraitValidation.IsValid(traits));
        }

        var all = new Traits
        {
            EyeColor = EyeColor.Hazel,
            HairColor = HairColor.Ginger,
            Handedness = Handedness.Ambidextrous,
            Morphotype = Morphotype.Endomorph,
            Aptitude = Aptitude.Physical,
            HeightPotential = HeightPotential.Tall,
            Sex = Sex.Male,
            Temperament = Temperament.Violent
        };

        Assert.Empty(TraitValidation.Validate(all));
    }

    [Fact]
    public void ValidationRejectsUndefinedEnumValues()
    {
        var invalid = new Traits { EyeColor = (EyeColor)200 };

        Assert.False(TraitValidation.IsValid(invalid));
        Assert.Contains(TraitValidation.Validate(invalid), error => error.Contains("EyeColor"));
        Assert.Throws<InvalidOperationException>(() => TraitValidation.EnsureValid(invalid, 42UL));
    }

    [Fact]
    public void PrototypeMappingCoversEveryInputColumn()
    {
        var traits = new Traits
        {
            EyeColor = EyeColor.Green,
            HairColor = HairColor.Ginger,
            Handedness = Handedness.Ambidextrous,
            Morphotype = Morphotype.Endomorph,
            Aptitude = Aptitude.Science,
            HeightPotential = HeightPotential.Tall,
            Sex = Sex.Female,
            Temperament = Temperament.Violent
        };

        var row = TraitMapping.ToPrototypeRow(traits, 79);

        Assert.Equal(TraitMapping.PrototypeColumns.Count, row.Count);
        foreach (var column in TraitMapping.PrototypeColumns)
            Assert.True(row.ContainsKey(column), $"missing prototype column {column}");

        Assert.Equal("Ginger", row[TraitMapping.HairTypeColumn]);
        Assert.Equal("Science", row[TraitMapping.IntelligenceColumn]);
        Assert.Equal("Violent", row[TraitMapping.BehaviorPropensionColumn]);
        Assert.Equal(79f, row[TraitMapping.AgeColumn]);
    }

    [Fact]
    public void PrefixedMappingProducesPrototypePairColumns()
    {
        var traits = new Traits { EyeColor = EyeColor.Blue };
        var row = TraitMapping.ToPrototypeRow("F", traits, 30).ToDictionary(pair => pair.Key, pair => pair.Value);

        Assert.Equal("Blue", row["F_EyeColor"]);
        Assert.Equal("Age", TraitMapping.AgeColumn);
    }

    private static void AssertRoundTrip<T>() where T : struct, Enum
    {
        foreach (var value in Enum.GetValues<T>())
        {
            var numeric = Convert.ToInt32(value);
            Assert.Equal(value, Enum.ToObject(typeof(T), numeric));
        }
    }
}
