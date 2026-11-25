using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.Family;

public enum PregnancyError
{
    None,
    SelfParent,
    MissingGenos,
    NotAdult,
    Dependent,
    ActivePregnancy,
    InvalidTick,
    UnsupportedStatus
}

/// <summary>
/// Creates and resolves pregnancy entities. The index is updated in the same
/// apply phase; history is retained after completion, loss or cancellation.
/// </summary>
public sealed class PregnancyService
{
    private readonly World world;
    private readonly PregnancyIndex index;
    private ulong fallbackNetworkId = 3_000_000_000;

    public PregnancyService(World world, PregnancyIndex index)
    {
        this.world = world;
        this.index = index;
        index.AttachWorld(world);
    }

    public Func<Entity, NetworkId>? NetworkIdAllocator { get; set; }
    public Action<Entity>? NetworkIdRemover { get; set; }

    public bool TryStart(
        Entity carryingParent,
        Entity otherParent,
        long conceivedTick,
        long dueTick,
        int healthAtConception,
        PregnancySource source,
        ReproductionOptions? options,
        out Entity pregnancy,
        out PregnancyError error)
    {
        pregnancy = Entity.Null;
        options ??= ReproductionOptions.Default;

        if (carryingParent == otherParent)
        {
            error = PregnancyError.SelfParent;
            return false;
        }

        if (!world.IsAlive(carryingParent) || !world.IsAlive(otherParent) ||
            world.Has<Dead>(carryingParent) || world.Has<Dead>(otherParent))
        {
            error = PregnancyError.MissingGenos;
            return false;
        }

        if (world.Has<Alive>(carryingParent) &&
            world.Get<Alive>(carryingParent).Age < options.MinimumConceptionAge)
        {
            error = PregnancyError.NotAdult;
            return false;
        }

        if (IsDependent(world, carryingParent) || IsDependent(world, otherParent))
        {
            error = PregnancyError.Dependent;
            return false;
        }

        if (index.TryGetActive(carryingParent, out _))
        {
            error = PregnancyError.ActivePregnancy;
            return false;
        }

        if (dueTick <= conceivedTick)
        {
            error = PregnancyError.InvalidTick;
            return false;
        }

        var data = new Pregnancy
        {
            CarryingParent = carryingParent,
            OtherParent = otherParent,
            ConceivedTick = conceivedTick,
            DueTick = dueTick,
            RiskBasisPoints = 0,
            HealthAtConception = healthAtConception,
            Status = PregnancyStatus.Active,
            Source = source,
            LastProgressTick = conceivedTick,
            ChildrenExpected = 1,
            CarePaid = Money.Zero,
            CareArrears = Money.Zero
        };

        pregnancy = world.Create(data);
        var networkId = NetworkIdAllocator?.Invoke(pregnancy) ?? new NetworkId(fallbackNetworkId++);
        if (networkId.Value != 0 && !world.Has<NetworkId>(pregnancy))
            world.Add(pregnancy, networkId);
        index.OnStarted(pregnancy, data);
        error = PregnancyError.None;
        return true;
    }

    public bool Progress(Entity pregnancy, long tick, int riskBasisPoints)
    {
        if (!TryRead(pregnancy, out var data))
            return false;
        if (data.Status is PregnancyStatus.Completed or PregnancyStatus.Lost or PregnancyStatus.Cancelled)
            return false;

        data.RiskBasisPoints = Math.Clamp(riskBasisPoints, 0, 1000);
        data.LastProgressTick = tick;
        world.Get<Pregnancy>(pregnancy) = data;
        return true;
    }

    public bool SetReady(Entity pregnancy, long tick)
    {
        if (!TryRead(pregnancy, out var data))
            return false;
        if (data.Status == PregnancyStatus.ReadyToBirth)
            return true;
        if (data.Status != PregnancyStatus.Active)
            return false;

        data.Status = PregnancyStatus.ReadyToBirth;
        data.LastProgressTick = tick;
        world.Get<Pregnancy>(pregnancy) = data;
        return true;
    }

    public bool RecordCare(
        Entity pregnancy,
        Money paid,
        Money arrears,
        bool missedCare,
        int complications,
        long tick)
    {
        if (!TryRead(pregnancy, out var data))
            return false;
        if (data.Status is PregnancyStatus.Completed or PregnancyStatus.Lost or PregnancyStatus.Cancelled)
            return false;

        data.CarePaid += paid;
        data.CareArrears += arrears;
        data.CareMonths++;
        if (missedCare)
            data.MissedCareMonths++;
        data.Complications = Math.Max(0, complications);
        data.LastProgressTick = tick;
        world.Get<Pregnancy>(pregnancy) = data;
        return true;
    }

    public bool Resolve(Entity pregnancy, PregnancyStatus status, long tick)
    {
        if (status is PregnancyStatus.Active or PregnancyStatus.ReadyToBirth)
            return false;
        if (!TryRead(pregnancy, out var data))
            return false;
        if (data.Status is PregnancyStatus.Completed or PregnancyStatus.Lost or PregnancyStatus.Cancelled)
            return false;

        data.Status = status;
        data.ResolvedTick = tick;
        data.LastProgressTick = tick;
        world.Get<Pregnancy>(pregnancy) = data;
        index.OnResolved(pregnancy, data);
        return true;
    }

    public bool Complete(Entity pregnancy, long tick) =>
        Resolve(pregnancy, PregnancyStatus.Completed, tick);

    public bool Lose(Entity pregnancy, long tick) =>
        Resolve(pregnancy, PregnancyStatus.Lost, tick);

    public bool Cancel(Entity pregnancy, long tick) =>
        Resolve(pregnancy, PregnancyStatus.Cancelled, tick);

    public bool TryRead(Entity pregnancy, out Pregnancy data)
    {
        data = default;
        if (!world.IsAlive(pregnancy) || !world.Has<Pregnancy>(pregnancy))
            return false;
        data = world.Get<Pregnancy>(pregnancy);
        return true;
    }

    public static bool IsDependent(World world, Entity genos) =>
        world.Has<DependentChild>(genos) ||
        (world.Has<HouseholdMember>(genos) && world.Get<HouseholdMember>(genos).Role == HouseholdRole.Child);
}
