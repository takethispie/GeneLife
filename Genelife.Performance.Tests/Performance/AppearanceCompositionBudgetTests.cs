using System.Diagnostics;
using Genelife.Appearance;
using Genelife.Components.Gen;

namespace Genelife.UnitTests.Performance;

/// <summary>
/// Release evidence for the shared-atlas appearance contract: composing the same
/// 2,000 living sims the 2k decade scenario runs must stay inside one 60 Hz frame.
/// The composer is pure and allocates no textures; the client reuses the same
/// layer contract to build sprites.
/// </summary>
[Trait("Category", "Performance")]
public class AppearanceCompositionBudgetTests
{
    [Fact]
    public void TwoThousandAppearanceCompositionsFitInsideOneFrame()
    {
        var descriptors = new AppearanceDescriptor[2_000];
        for (var i = 0; i < descriptors.Length; i++)
        {
            var traits = new Traits
            {
                EyeColor = (EyeColor)(i % 4),
                HairColor = (HairColor)(i % 5),
                Handedness = Handedness.Right,
                Morphotype = (Morphotype)(i % 3),
                Aptitude = (Aptitude)(i % 5),
                HeightPotential = (HeightPotential)(i % 4),
                Sex = i % 2 == 0 ? Sex.Female : Sex.Male,
                Temperament = (Temperament)(i % 3),
                SkinTone = (SkinTone)(i % 6),
                FaceShape = (FaceShape)(i % 5)
            };
            descriptors[i] = AppearanceDerivation.Derive(traits, 5 + i % 80, (ulong)i);
        }

        for (var i = 0; i < 50; i++)
            AppearanceComposer.Compose(descriptors[i]);

        var started = Stopwatch.GetTimestamp();
        var checksum = 0;
        for (var i = 0; i < descriptors.Length; i++)
        {
            var layers = AppearanceComposer.Compose(descriptors[i]);
            checksum += layers.BodyPart.Length + layers.FacePart.Length + layers.HairPart.Length;
        }
        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        Console.WriteLine($"[appearance] composed {descriptors.Length} descriptors in {elapsedMs:F2}ms");
        Assert.True(checksum > 0);
        Assert.True(elapsedMs < 16.7,
            $"composing 2,000 appearances took {elapsedMs:F2}ms");
    }
}
