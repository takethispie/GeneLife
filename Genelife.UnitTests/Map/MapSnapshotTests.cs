using Genelife.Api.Services;
using Genelife.Map;

namespace Genelife.UnitTests.Map;

public class MapSnapshotTests
{
    [Fact]
    public void BuildMapSnapshot_run_length_encoding_covers_every_tile_exactly()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();

        var snapshot = engine.BuildMapSnapshot();
        var map = engine.MapContext!.Map;
        var layout = map.Layout!;

        Assert.Equal("world", snapshot.MapId);
        Assert.Equal(snapshot.Width * snapshot.Height, snapshot.Tiles.Sum(run => run.Length));

        var flags = new Dictionary<(int X, int Y), string>();
        foreach (var run in snapshot.Tiles)
        {
            Assert.InRange(run.Length, 1, snapshot.Width);
            for (var offset = 0; offset < run.Length; offset++)
                flags[(run.X + offset, run.Y)] = run.Flags;
        }

        for (var y = snapshot.MinY; y <= snapshot.MaxY; y++)
        {
            for (var x = snapshot.MinX; x <= snapshot.MaxX; x++)
            {
                Assert.True(flags.TryGetValue((x, y), out var actual), $"Missing tile {x},{y}");
                Assert.Equal(layout.FlagsAt(new TileCoord(x, y)).ToString(), actual);
            }
        }

        Assert.Equal(map.Placements.Count, snapshot.Buildings.Count);
    }
}
