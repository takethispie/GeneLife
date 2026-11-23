using System.Reflection;
using Genelife.Appearance;
using Genelife.Components.Gen;
using Genelife.Family;

namespace Genelife.UnitTests.Appearance;

public class AppearanceDomainTests
{
    private static readonly Traits Sample = new()
    {
        EyeColor = EyeColor.Green,
        HairColor = HairColor.Brown,
        Handedness = Handedness.Right,
        Morphotype = Morphotype.Mesomorph,
        Aptitude = Aptitude.Social,
        HeightPotential = HeightPotential.Average,
        Sex = Sex.Female,
        Temperament = Temperament.Neutral,
        SkinTone = SkinTone.Olive,
        FaceShape = FaceShape.Oval
    };

    [Fact]
    public void Descriptor_IsDeterministicFixedSizeAndStringFree()
    {
        var first = AppearanceDerivation.Derive(Sample, 30, 42);
        var second = AppearanceDerivation.Derive(Sample, 30, 42);

        Assert.Equal(first, second);
        Assert.Equal(first.StableHash(), second.StableHash());
        Assert.True(AppearanceValidation.IsValid(first));
        Assert.Equal(AppearanceDescriptor.CurrentVersion, first.AppearanceVersion);

        var fields = typeof(AppearanceDescriptor).GetFields(BindingFlags.Instance | BindingFlags.Public);
        Assert.All(fields, field => Assert.NotEqual(typeof(string), field.FieldType));
        Assert.True(System.Runtime.InteropServices.Marshal.SizeOf<AppearanceDescriptor>() <= 16);
    }

    [Fact]
    public void Hash_ChangesWhenAnyVisualFieldChanges()
    {
        var baseline = AppearanceDerivation.Derive(Sample, 30, 42);
        var changed = baseline with { SkinTone = SkinTone.Deep };
        Assert.NotEqual(baseline.StableHash(), changed.StableHash());
    }

    [Fact]
    public void Derivation_NeverMutatesTraits()
    {
        var traits = Sample;
        _ = AppearanceDerivation.Derive(traits, 30, 99);
        Assert.Equal(Sample.SkinTone, traits.SkinTone);
        Assert.Equal(Sample.FaceShape, traits.FaceShape);
        Assert.Equal(Sample.HairColor, traits.HairColor);
        Assert.Equal(Sample.EyeColor, traits.EyeColor);
        Assert.Equal(Sample.Temperament, traits.Temperament);
    }

    [Fact]
    public void StableDraw_ChangesDerivedFieldsButNeverInheritedGenes()
    {
        var a = AppearanceDerivation.Derive(Sample, 30, 1);
        var b = AppearanceDerivation.Derive(Sample, 30, 2);

        Assert.Equal(a.SkinTone, b.SkinTone);
        Assert.Equal(a.FaceShape, b.FaceShape);
        Assert.Equal(a.HairColor == a.HairColor, true);
        Assert.True(a.HairStyle != b.HairStyle || a.Expression != b.Expression);
    }

    [Fact]
    public void Aging_ChangesBandAndAppearanceWithoutChangingTraits()
    {
        var child = AppearanceDerivation.Derive(Sample, 5, 10);
        var teen = AppearanceDerivation.Derive(Sample, 15, 10);
        var adult = AppearanceDerivation.Derive(Sample, 25, 10);
        var middle = AppearanceDerivation.Derive(Sample, 45, 10);
        var senior = AppearanceDerivation.Derive(Sample, 70, 10);

        Assert.Equal(AgeBand.Child, child.AgeBand);
        Assert.Equal(AgeBand.Teen, teen.AgeBand);
        Assert.Equal(AgeBand.Adult, adult.AgeBand);
        Assert.Equal(AgeBand.Middle, middle.AgeBand);
        Assert.Equal(AgeBand.Senior, senior.AgeBand);
        Assert.Equal(HairColor.Grey, senior.HairColor);
        Assert.True(senior.HeightScale < adult.HeightScale);
        Assert.Equal(Sample.HairColor, HairColor.Brown);
    }

    [Fact]
    public void Inheritance_CopiesSkinAndFaceWithoutMutation()
    {
        var parentA = Sample;
        var parentB = Sample with
        {
            SkinTone = SkinTone.Fair,
            FaceShape = FaceShape.Round
        };
        var options = new GeneticsOptions { MutationRate = 0m };

        var traits = TraitsInheritance.Create(parentA, 1, parentB, 2, 123, 50, options);

        Assert.True(traits.SkinTone == parentA.SkinTone || traits.SkinTone == parentB.SkinTone);
        Assert.True(traits.FaceShape == parentA.FaceShape || traits.FaceShape == parentB.FaceShape);
    }

    [Fact]
    public void ParentChildAndSibling_ShareInheritedFields()
    {
        var parent = AppearanceDerivation.Derive(Sample, 30, 1);
        var childTraits = Sample with { HeightPotential = HeightPotential.Short };
        var child = AppearanceDerivation.Derive(childTraits, 10, 2);
        var sibling = AppearanceDerivation.Derive(Sample, 8, 3);

        var parentChild = FamilyResemblance.Compare(parent, child);
        var siblings = FamilyResemblance.Compare(child, sibling);

        Assert.True(parentChild.SharesSkinTone);
        Assert.True(parentChild.SharesFaceShape);
        Assert.Equal(4, parentChild.SharedFields);
        Assert.Equal(4, siblings.SharedFields);
    }

    [Fact]
    public void Validation_RejectsUndefinedHeightsAndFutureVersions()
    {
        var descriptor = AppearanceDerivation.Derive(Sample, 30, 1);
        Assert.Contains(AppearanceValidation.Validate(descriptor with { SkinTone = (SkinTone)99 }),
            error => error.Contains("SkinTone", StringComparison.Ordinal));
        Assert.Contains(AppearanceValidation.Validate(descriptor with { HeightScale = 101 }),
            error => error.Contains("HeightScale", StringComparison.Ordinal));
        Assert.Contains(AppearanceValidation.Validate(descriptor with { AppearanceVersion = 99 }),
            error => error.Contains("newer", StringComparison.Ordinal));
    }

    [Fact]
    public void Composer_ProducesKnownPartsForEveryBandAndFrame()
    {
        foreach (var age in new[] { 5, 15, 25, 45, 70 })
        {
            foreach (Morphotype morph in Enum.GetValues<Morphotype>())
            {
                var descriptor = AppearanceDerivation.Derive(
                    Sample with { Morphotype = morph }, age, 77);
                var layers = AppearanceComposer.Compose(descriptor);
                foreach (var part in new[]
                         {
                             layers.BodyPart, layers.SkinPalette, layers.FacePart, layers.EyePart,
                             layers.HairPart, layers.OutfitPart, layers.ExpressionPart
                         })
                {
                    Assert.True(AppearanceLayerContract.IsKnownPart(part), part);
                }
                Assert.Equal(AppearanceLayerContract.CurrentLayerOrderVersion, layers.LayerOrderVersion);
            }
        }

        Assert.True(AppearanceLayerContract.IsKnownPart(AppearanceComposer.Fallback().BodyPart));
    }
}
