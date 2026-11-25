using Genelife.Api.DTOs;
using Genelife.Api.Snapshots;

namespace Genelife.UnitTests.Snapshots;

public class AoiFilterTests
{
    private static List<GenosStatus> CreateGenoses(int count, float spacing, float startX = 0f, float startY = 0f)
    {
        var genoses = new List<GenosStatus>();
        for (int i = 0; i < count; i++)
        {
            genoses.Add(new GenosStatus
            {
                NetworkId = (ulong)i,
                Name = $"Genos_{i}",
                X = startX + i * spacing,
                Y = startY,
                Hunger = 50f,
                Energy = 50f,
                Hygiene = 50f,
                Bladder = 50f,
                CurrentAction = "Idle",
                TimeRemaining = 0f,
            });
        }
        return genoses;
    }

    [Fact]
    public void FilterByRadius_IncludesEntitiesWithinRadius()
    {
        var genoses = CreateGenoses(5, spacing: 5f);
        var snapshot = new WorldSnapshot(1, 1, genoses, new List<CompanyStatus>());

        var results = AoiFilter.FilterByRadius(snapshot, 0f, 0f, 10f);

        Assert.Equal(3, results.Count);
        Assert.Equal("Genos_0", results[0].Name);
        Assert.Equal("Genos_1", results[1].Name);
        Assert.Equal("Genos_2", results[2].Name);
    }

    [Fact]
    public void FilterByRadius_ExcludesEntitiesOutsideRadius()
    {
        var genoses = CreateGenoses(10, spacing: 10f, startX: -50f);
        var snapshot = new WorldSnapshot(1, 1, genoses, new List<CompanyStatus>());

        var results = AoiFilter.FilterByRadius(snapshot, 0f, 0f, 12f);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void FilterByRadius_EmptyResult_WhenNothingNearby()
    {
        var genoses = CreateGenoses(3, spacing: 5f, startX: 100f, startY: 100f);
        var snapshot = new WorldSnapshot(1, 1, genoses, new List<CompanyStatus>());

        var results = AoiFilter.FilterByRadius(snapshot, 0f, 0f, 10f);

        Assert.Empty(results);
    }

    [Fact]
    public void FilterByRadius_AllIncluded_WhenRadiusCoversAll()
    {
        var genoses = CreateGenoses(5, spacing: 5f);
        var snapshot = new WorldSnapshot(1, 1, genoses, new List<CompanyStatus>());

        var results = AoiFilter.FilterByRadius(snapshot, 0f, 0f, 100f);

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void FilterByRadius_ReturnsAll_WhenEmptySnapshot()
    {
        var snapshot = new WorldSnapshot(1, 1, new List<GenosStatus>(), new List<CompanyStatus>());
        var results = AoiFilter.FilterByRadius(snapshot, 0f, 0f, 10f);
        Assert.Empty(results);
    }

    [Fact]
    public void FilterByRadius_IncludesEntityAtExactlyRadius()
    {
        var genoses = new List<GenosStatus>
        {
            new() { Name = "edge", X = 10f, Y = 0f },
            new() { Name = "outside", X = 10.01f, Y = 0f },
        };
        var snapshot = new WorldSnapshot(1, 1, genoses, new List<CompanyStatus>());

        var results = AoiFilter.FilterByRadius(snapshot, 0f, 0f, 10f);

        Assert.Single(results);
        Assert.Equal("edge", results[0].Name);
    }

    [Fact]
    public void FilterSnapshotByRadius_ReturnsFilteredSnapshot()
    {
        var genoses = CreateGenoses(10, spacing: 10f);
        var snapshot = new WorldSnapshot(42, 1, genoses, new List<CompanyStatus>());

        var filtered = AoiFilter.FilterSnapshotByRadius(snapshot, 0f, 0f, 15f);

        Assert.Equal(42, filtered.Tick);
        Assert.Equal(1, filtered.Version);
        Assert.Equal(2, filtered.Genoses.Count);
        Assert.Same(snapshot.Companies, filtered.Companies);
    }

    [Fact]
    public void FilterByRadius_DiagonalPositions()
    {
        var genoses = new List<GenosStatus>
        {
            new() { Name = "center", X = 0f, Y = 0f },
            new() { Name = "diagonal", X = 7.07f, Y = 7.07f },
            new() { Name = "far", X = 7.5f, Y = 7.5f },
        };
        var snapshot = new WorldSnapshot(1, 1, genoses, new List<CompanyStatus>());

        var results = AoiFilter.FilterByRadius(snapshot, 0f, 0f, 10f);

        Assert.Equal(2, results.Count);
        Assert.Equal("center", results[0].Name);
        Assert.Equal("diagonal", results[1].Name);
    }
}