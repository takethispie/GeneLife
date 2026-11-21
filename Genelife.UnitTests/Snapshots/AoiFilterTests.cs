using Genelife.Api.DTOs;
using Genelife.Api.Snapshots;

namespace Genelife.UnitTests.Snapshots;

public class AoiFilterTests
{
    private static List<SimStatus> CreateSims(int count, float spacing, float startX = 0f, float startY = 0f)
    {
        var sims = new List<SimStatus>();
        for (int i = 0; i < count; i++)
        {
            sims.Add(new SimStatus
            {
                NetworkId = (ulong)i,
                Name = $"Sim_{i}",
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
        return sims;
    }

    [Fact]
    public void FilterByRadius_IncludesEntitiesWithinRadius()
    {
        var sims = CreateSims(5, spacing: 5f);
        var snapshot = new WorldSnapshot(1, 1, sims, new List<CompanyStatus>());

        var results = AoiFilter.FilterByRadius(snapshot, 0f, 0f, 10f);

        Assert.Equal(3, results.Count);
        Assert.Equal("Sim_0", results[0].Name);
        Assert.Equal("Sim_1", results[1].Name);
        Assert.Equal("Sim_2", results[2].Name);
    }

    [Fact]
    public void FilterByRadius_ExcludesEntitiesOutsideRadius()
    {
        var sims = CreateSims(10, spacing: 10f, startX: -50f);
        var snapshot = new WorldSnapshot(1, 1, sims, new List<CompanyStatus>());

        var results = AoiFilter.FilterByRadius(snapshot, 0f, 0f, 12f);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void FilterByRadius_EmptyResult_WhenNothingNearby()
    {
        var sims = CreateSims(3, spacing: 5f, startX: 100f, startY: 100f);
        var snapshot = new WorldSnapshot(1, 1, sims, new List<CompanyStatus>());

        var results = AoiFilter.FilterByRadius(snapshot, 0f, 0f, 10f);

        Assert.Empty(results);
    }

    [Fact]
    public void FilterByRadius_AllIncluded_WhenRadiusCoversAll()
    {
        var sims = CreateSims(5, spacing: 5f);
        var snapshot = new WorldSnapshot(1, 1, sims, new List<CompanyStatus>());

        var results = AoiFilter.FilterByRadius(snapshot, 0f, 0f, 100f);

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void FilterByRadius_ReturnsAll_WhenEmptySnapshot()
    {
        var snapshot = new WorldSnapshot(1, 1, new List<SimStatus>(), new List<CompanyStatus>());
        var results = AoiFilter.FilterByRadius(snapshot, 0f, 0f, 10f);
        Assert.Empty(results);
    }

    [Fact]
    public void FilterByRadius_IncludesEntityAtExactlyRadius()
    {
        var sims = new List<SimStatus>
        {
            new() { Name = "edge", X = 10f, Y = 0f },
            new() { Name = "outside", X = 10.01f, Y = 0f },
        };
        var snapshot = new WorldSnapshot(1, 1, sims, new List<CompanyStatus>());

        var results = AoiFilter.FilterByRadius(snapshot, 0f, 0f, 10f);

        Assert.Single(results);
        Assert.Equal("edge", results[0].Name);
    }

    [Fact]
    public void FilterSnapshotByRadius_ReturnsFilteredSnapshot()
    {
        var sims = CreateSims(10, spacing: 10f);
        var snapshot = new WorldSnapshot(42, 1, sims, new List<CompanyStatus>());

        var filtered = AoiFilter.FilterSnapshotByRadius(snapshot, 0f, 0f, 15f);

        Assert.Equal(42, filtered.Tick);
        Assert.Equal(1, filtered.Version);
        Assert.Equal(2, filtered.Sims.Count);
        Assert.Same(snapshot.Companies, filtered.Companies);
    }

    [Fact]
    public void FilterByRadius_DiagonalPositions()
    {
        var sims = new List<SimStatus>
        {
            new() { Name = "center", X = 0f, Y = 0f },
            new() { Name = "diagonal", X = 7.07f, Y = 7.07f },
            new() { Name = "far", X = 7.5f, Y = 7.5f },
        };
        var snapshot = new WorldSnapshot(1, 1, sims, new List<CompanyStatus>());

        var results = AoiFilter.FilterByRadius(snapshot, 0f, 0f, 10f);

        Assert.Equal(2, results.Count);
        Assert.Equal("center", results[0].Name);
        Assert.Equal("diagonal", results[1].Name);
    }
}