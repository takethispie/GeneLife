using System.Numerics;
using Arch.Core;
using Genelife.Map;
using Genelife.Application.Systems;

namespace Genelife.UnitTests.Systems;

public class SpatialIndexTests
{
    private static readonly WorldBounds DefaultBounds = new(halfExtent: 1000f);

    [Fact]
    public void Add_PutsEntityInCorrectCell()
    {
        var index = new SpatialIndex(10f, DefaultBounds);
        var world = World.Create();
        var entity = world.Create();

        index.Add(entity, new Vector2(25f, 35f));

        Assert.Equal(1, index.EntityCount);
        Assert.Equal(1, index.CellCount);
        var inCell = index.GetEntitiesInCell(2, 3).ToList();
        Assert.Single(inCell);
        Assert.Equal(entity, inCell[0]);
    }

    [Fact]
    public void Add_MultipleEntitiesSameCell_GroupsThem()
    {
        var index = new SpatialIndex(10f, DefaultBounds);
        var world = World.Create();
        var e1 = world.Create();
        var e2 = world.Create();

        index.Add(e1, new Vector2(5f, 5f));
        index.Add(e2, new Vector2(9f, 9f));

        Assert.Equal(2, index.EntityCount);
        Assert.Equal(1, index.CellCount);
        var inCell = index.GetEntitiesInCell(0, 0).ToList();
        Assert.Equal(2, inCell.Count);
    }

    [Fact]
    public void Add_MultipleEntitiesDifferentCells()
    {
        var index = new SpatialIndex(10f, DefaultBounds);
        var world = World.Create();
        var e1 = world.Create();
        var e2 = world.Create();

        index.Add(e1, new Vector2(5f, 5f));
        index.Add(e2, new Vector2(25f, 25f));

        Assert.Equal(2, index.EntityCount);
        Assert.Equal(2, index.CellCount);
    }

    [Fact]
    public void Remove_DeletesEntityFromGrid()
    {
        var index = new SpatialIndex(10f, DefaultBounds);
        var world = World.Create();
        var entity = world.Create();

        index.Add(entity, new Vector2(5f, 5f));
        Assert.Equal(1, index.EntityCount);

        index.Remove(entity);

        Assert.Equal(0, index.EntityCount);
        Assert.Equal(0, index.CellCount);
        var inCell = index.GetEntitiesInCell(0, 0).ToList();
        Assert.Empty(inCell);
    }

    [Fact]
    public void Remove_NonExistentEntity_DoesNotThrow()
    {
        var index = new SpatialIndex(10f, DefaultBounds);
        var world = World.Create();
        var entity = world.Create();

        index.Remove(entity);

        Assert.Equal(0, index.EntityCount);
    }

    [Fact]
    public void Move_UpdatesCell()
    {
        var index = new SpatialIndex(10f, DefaultBounds);
        var world = World.Create();
        var entity = world.Create();

        index.Add(entity, new Vector2(5f, 5f));
        index.Move(entity, new Vector2(25f, 25f));

        Assert.Equal(1, index.EntityCount);
        var oldCell = index.GetEntitiesInCell(0, 0).ToList();
        Assert.Empty(oldCell);
        var newCell = index.GetEntitiesInCell(2, 2).ToList();
        Assert.Single(newCell);
        Assert.Equal(entity, newCell[0]);
    }

    [Fact]
    public void Move_WithinSameCell_Noop()
    {
        var index = new SpatialIndex(10f, DefaultBounds);
        var world = World.Create();
        var entity = world.Create();

        index.Add(entity, new Vector2(5f, 5f));
        index.Move(entity, new Vector2(9f, 9f));

        Assert.Equal(1, index.EntityCount);
        Assert.Equal(1, index.CellCount);
        var inCell = index.GetEntitiesInCell(0, 0).ToList();
        Assert.Single(inCell);
    }

    [Fact]
    public void Move_UntrackedEntity_DoesNotThrow()
    {
        var index = new SpatialIndex(10f, DefaultBounds);
        var world = World.Create();
        var entity = world.Create();

        index.Move(entity, new Vector2(25f, 25f));
        Assert.Equal(0, index.EntityCount);
    }

    [Fact]
    public void Clear_RemovesAllEntities()
    {
        var index = new SpatialIndex(10f, DefaultBounds);
        var world = World.Create();
        index.Add(world.Create(), new Vector2(5f, 5f));
        index.Add(world.Create(), new Vector2(15f, 15f));
        index.Add(world.Create(), new Vector2(25f, 25f));

        index.Clear();

        Assert.Equal(0, index.EntityCount);
        Assert.Equal(0, index.CellCount);
    }

    [Fact]
    public void GetNeighbors_FindsEntitiesWithinRadius()
    {
        var index = new SpatialIndex(10f, DefaultBounds);
        var world = World.Create();
        var e1 = world.Create();
        var e2 = world.Create();
        var e3 = world.Create();

        index.Add(e1, new Vector2(0f, 0f));
        index.Add(e2, new Vector2(8f, 0f));
        index.Add(e3, new Vector2(20f, 0f));

        var positions = new Dictionary<Entity, Vector2>
        {
            { e1, new Vector2(0f, 0f) },
            { e2, new Vector2(8f, 0f) },
            { e3, new Vector2(20f, 0f) },
        };

        var neighbors = index.GetNeighbors(new Vector2(0f, 0f), 10f, e => positions[e]);

        Assert.Equal(2, neighbors.Count);
        Assert.Equal(e1, neighbors[0].Entity);
        Assert.Equal(e2, neighbors[1].Entity);
    }

    [Fact]
    public void GetNeighbors_EmptyResult_WhenNothingInRadius()
    {
        var index = new SpatialIndex(10f, DefaultBounds);
        var world = World.Create();
        var entity = world.Create();

        index.Add(entity, new Vector2(100f, 100f));

        var positions = new Dictionary<Entity, Vector2>
        {
            { entity, new Vector2(100f, 100f) },
        };

        var neighbors = index.GetNeighbors(new Vector2(0f, 0f), 5f, e => positions[e]);
        Assert.Empty(neighbors);
    }

    [Fact]
    public void GetNeighbors_SortsByDistance()
    {
        var index = new SpatialIndex(10f, DefaultBounds);
        var world = World.Create();
        var e1 = world.Create();
        var e2 = world.Create();
        var e3 = world.Create();

        index.Add(e1, new Vector2(10f, 0f));
        index.Add(e2, new Vector2(3f, 0f));
        index.Add(e3, new Vector2(7f, 0f));

        var positions = new Dictionary<Entity, Vector2>
        {
            { e1, new Vector2(10f, 0f) },
            { e2, new Vector2(3f, 0f) },
            { e3, new Vector2(7f, 0f) },
        };

        var neighbors = index.GetNeighbors(new Vector2(0f, 0f), 15f, e => positions[e]);

        Assert.Equal(3, neighbors.Count);
        Assert.Equal(e2, neighbors[0].Entity);
        Assert.Equal(e3, neighbors[1].Entity);
        Assert.Equal(e1, neighbors[2].Entity);
    }

    [Fact]
    public void GetEntitiesInAABB_ReturnsEntitiesInsideBounds()
    {
        var index = new SpatialIndex(10f, DefaultBounds);
        var world = World.Create();
        var e1 = world.Create();
        var e2 = world.Create();
        var e3 = world.Create();

        index.Add(e1, new Vector2(5f, 5f));
        index.Add(e2, new Vector2(25f, 25f));
        index.Add(e3, new Vector2(50f, 50f));

        var results = index.GetEntitiesInAABB(new Vector2(0f, 0f), new Vector2(30f, 30f)).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(e1, results);
        Assert.Contains(e2, results);
    }

    [Fact]
    public void SpawnMoveDestroy_Churn_KeepsGridConsistent()
    {
        var index = new SpatialIndex(10f, DefaultBounds);
        var world = World.Create();
        var entities = new List<Entity>();

        for (int i = 0; i < 100; i++)
        {
            var e = world.Create();
            entities.Add(e);
            index.Add(e, new Vector2(i * 3f % 200f - 100f, i * 7f % 200f - 100f));
        }

        Assert.Equal(100, index.EntityCount);

        for (int i = 0; i < 50; i++)
        {
            index.Move(entities[i], new Vector2(i * 4f % 200f - 100f, i * 6f % 200f - 100f));
        }

        Assert.Equal(100, index.EntityCount);

        for (int i = 0; i < 50; i++)
        {
            index.Remove(entities[i]);
        }

        Assert.Equal(50, index.EntityCount);

        for (int i = 0; i < 50; i++)
        {
            index.Remove(entities[50 + i]);
        }

        Assert.Equal(0, index.EntityCount);
        Assert.Equal(0, index.CellCount);
    }

    [Fact]
    public void WorldBounds_ClampsPositionsOutside()
    {
        var bounds = new WorldBounds(-500f, -500f, 500f, 500f);
        var index = new SpatialIndex(10f, bounds);
        var world = World.Create();
        var entity = world.Create();

        index.Add(entity, new Vector2(2000f, -2000f));

        Assert.Equal(1, index.EntityCount);

        var positions = new Dictionary<Entity, Vector2>
        {
            { entity, new Vector2(500f, -500f) },
        };

        var neighbors = index.GetNeighbors(new Vector2(500f, -500f), 1f, e => positions[e]);
        Assert.Single(neighbors);
    }

    [Fact]
    public void CellSize_ZeroOrNegative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialIndex(0f, DefaultBounds));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialIndex(-1f, DefaultBounds));
    }

    [Fact]
    public void CreateDefault_ProducesUsableIndex()
    {
        var index = SpatialIndex.CreateDefault();
        Assert.Equal(10f, index.CellSize);
        Assert.Equal(0, index.EntityCount);

        var world = World.Create();
        var entity = world.Create();
        index.Add(entity, new Vector2(5f, 5f));
        Assert.Equal(1, index.EntityCount);
    }

    [Fact]
    public void GetOccupiedCellsInRadius_ReturnsOnlyOccupiedCells()
    {
        var index = new SpatialIndex(10f, DefaultBounds);
        var world = World.Create();
        index.Add(world.Create(), new Vector2(5f, 5f));
        index.Add(world.Create(), new Vector2(25f, 25f));

        var cells = index.GetOccupiedCellsInRadius(new Vector2(5f, 5f), 5f).ToList();
        Assert.Single(cells);

        cells = index.GetOccupiedCellsInRadius(new Vector2(5f, 5f), 50f).ToList();
        Assert.True(cells.Count >= 2);
    }

    [Fact]
    public void ThrowsException_OnInit_NegativeCellSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialIndex(-1f, DefaultBounds));
    }

    [Fact]
    public void LargeWorld_ManyEntities_HandlesChurn()
    {
        var index = SpatialIndex.CreateDefault();
        var world = World.Create();
        var entities = new List<Entity>();
        var rng = new Random(42);
        var positions = new Dictionary<Entity, Vector2>();

        for (int i = 0; i < 500; i++)
        {
            var e = world.Create();
            var pos = new Vector2((float)rng.NextDouble() * 500f - 250f, (float)rng.NextDouble() * 500f - 250f);
            entities.Add(e);
            positions[e] = pos;
            index.Add(e, pos);
        }

        Assert.Equal(500, index.EntityCount);

        for (int i = 0; i < 200; i++)
        {
            var e = entities[i];
            var newPos = new Vector2((float)rng.NextDouble() * 500f - 250f, (float)rng.NextDouble() * 500f - 250f);
            positions[e] = newPos;
            index.Move(e, newPos);
        }

        Assert.Equal(500, index.EntityCount);

        for (int i = 0; i < 100; i++)
        {
            index.Remove(entities[i]);
        }

        Assert.Equal(400, index.EntityCount);

        for (int i = 500; i < 600; i++)
        {
            var e = world.Create();
            var pos = new Vector2((float)rng.NextDouble() * 500f - 250f, (float)rng.NextDouble() * 500f - 250f);
            entities.Add(e);
            positions[e] = pos;
            index.Add(e, pos);
        }

        Assert.Equal(500, index.EntityCount);
    }
}