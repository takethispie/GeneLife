using System.Text.Json;
using Genelife.Api.DTOs;

namespace Genelife.UnitTests.Appearance;

public class AppearancePayloadTests
{
    [Fact]
    public void TwoThousandGenosStatusesWithAppearanceStayWithinPayloadBudget()
    {
        var genoses = Enumerable.Range(1, 2_000).Select(index => new GenosStatus
        {
            NetworkId = (ulong)index,
            Name = $"Genos {index}",
            Age = 20 + index % 50,
            X = index % 100,
            Y = index / 100,
            TileX = index % 100,
            TileY = index / 100,
            CurrentAction = "Idle",
            Appearance = new AppearanceDto
            {
                SkinTone = (byte)(index % 6),
                FaceShape = (byte)(index % 5),
                HairColor = (byte)(index % 5),
                HairStyle = (byte)(index % 7),
                EyeColor = (byte)(index % 5),
                BodyFrame = (byte)(index % 3),
                HeightScale = 80,
                AgeBand = 2,
                Expression = (byte)(index % 4),
                AppearanceVersion = 1
            }
        }).ToList();

        var full = JsonSerializer.SerializeToUtf8Bytes(genoses);
        var oneGenosDelta = JsonSerializer.SerializeToUtf8Bytes(new[] { genoses[0] });

        Assert.True(full.Length < 2_000_000, $"full={full.Length} bytes");
        Assert.True(oneGenosDelta.Length * 100 < full.Length,
            $"delta={oneGenosDelta.Length} full={full.Length}");
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(genoses[0].Appearance).Length < 240);
    }
}
