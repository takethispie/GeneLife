using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.Services;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Life;
using Genelife.Map;
using Genelife.Application.Systems;
using Genelife.Values;

namespace Genelife.Api.Repositories;

/// <summary>
/// Small administrative repository for school entities. Systems read the
/// component directly; this class exists for admin commands and tests.
/// </summary>
public sealed class SchoolRepository
{
    private readonly World world;
    private readonly NetworkIdRegistry networkIds;
    private readonly EconomyContext? economy;
    private readonly MapContext? map;

    public SchoolRepository(World world, NetworkIdRegistry networkIds, EconomyContext? economy = null, MapContext? map = null)
    {
        this.world = world;
        this.networkIds = networkIds;
        this.economy = economy;
        this.map = map;
    }

    public NetworkId Add(
        string name,
        string district,
        int capacity,
        float quality,
        Skill emphasis,
        Money monthlyCost,
        ulong ownerId = 0)
    {
        Entity owner = Entity.Null;
        if (ownerId != 0)
            networkIds.TryGetEntity(new NetworkId(ownerId), out owner);

        var entity = world.Create(new School(
            name,
            district,
            capacity,
            quality,
            emphasis,
            monthlyCost,
            owner));
        var id = networkIds.Allocate(entity);
        world.Add(entity, id);

        if (map is not null)
        {
            var placement = map.Placement.TryPlace(
                entity,
                id,
                BuildingKind.School,
                BuildingFootprints.ForSchool(capacity),
                district);
            if (!placement.Success)
            {
                networkIds.Remove(entity);
                world.Destroy(entity);
                throw new InvalidOperationException(
                    $"Unable to place school '{name}': {placement.Error} {placement.Message}");
            }
        }

        return id;
    }

    public bool Update(
        NetworkId id,
        string? name,
        int? capacity,
        float? quality,
        Skill? emphasis,
        Money? monthlyCost)
    {
        if (!networkIds.TryGetEntity(id, out var entity) ||
            !world.IsAlive(entity) || !world.Has<School>(entity))
            return false;

        ref var school = ref world.Get<School>(entity);
        if (!string.IsNullOrWhiteSpace(name))
            school.Name = name;
        if (capacity is { } newCapacity)
            school.Capacity = Math.Max(1, newCapacity);
        if (quality is { } newQuality)
            school.Quality = Math.Clamp(newQuality, 0f, 1f);
        if (emphasis is { } newEmphasis)
            school.Emphasis = newEmphasis;
        if (monthlyCost is { } cost)
            school.MonthlyCost = cost;
        return true;
    }

    public bool SetEnrollment(ulong genosId, ulong schoolId, bool enrolled)
    {
        if (!networkIds.TryGetEntity(new NetworkId(genosId), out var genos) ||
            !world.IsAlive(genos))
            return false;

        if (enrolled)
        {
            var educationStage = world.Has<Lifecycle>(genos)
                ? world.Get<Lifecycle>(genos).Stage == LifeStage.Education
                : world.Has<Alive>(genos) &&
                  LifecycleClassifier.Classify(world.Get<Alive>(genos).Age) == LifeStage.Education;
            if (!educationStage)
                return false;
        }

        if (!enrolled)
        {
            EnrollmentHelpers.ReleaseSeat(world, genos, economy?.Life.Index);
            return true;
        }

        if (!networkIds.TryGetEntity(new NetworkId(schoolId), out var schoolEntity) ||
            !world.IsAlive(schoolEntity) || !world.Has<School>(schoolEntity))
            return false;
        ref var target = ref world.Get<School>(schoolEntity);
        if (!target.HasCapacity)
            return false;

        if (world.Has<Enrollment>(genos))
        {
            var current = world.Get<Enrollment>(genos);
            if (!current.IsUnenrolled && current.School == schoolEntity)
                return true;
            EnrollmentHelpers.ReleaseSeat(world, genos, economy?.Life.Index);
        }

        target.Enrolled++;
        world.Add(genos, new Enrollment(schoolEntity, 0, 1f));
        economy?.Life.Index.OnEnrolled(schoolEntity, unenrolled: false);
        return true;
    }
}
