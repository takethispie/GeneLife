using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Shared apply-phase helpers for lifecycle transitions and death cleanup.
/// </summary>
public static class LifeSystemHelpers
{
    public static ulong NetworkIdOf(World world, Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;

    public static void RemoveEmployment(
        World world,
        EmploymentIndex index,
        EconomyContext? economy,
        Entity sim,
        long tick,
        bool retire = false)
    {
        if (!world.IsAlive(sim) || !world.Has<Employee>(sim))
            return;

        var employee = world.Get<Employee>(sim);
        world.Remove<Employee>(sim);
        index.Remove(sim);

        if (world.Has<WorkSchedule>(sim))
            world.Remove<WorkSchedule>(sim);
        if (world.Has<EmploymentRecord>(sim))
            world.Remove<EmploymentRecord>(sim);
        if (world.Has<CurrentAction>(sim))
            world.Get<CurrentAction>(sim).Type = Enums.ActionType.Idle;

        RepostSeat(world, economy, employee, tick);
        OfficeSeatAccounting.RefreshUsedSeats(world, employee.Office);

        if (retire)
        {
            economy?.Emit(new EmployeeRetired(
                NetworkIdOf(world, sim),
                NetworkIdOf(world, employee.Company),
                employee.Position,
                tick));
        }
    }

    private static void RepostSeat(World world, EconomyContext? economy, Employee employee, long tick)
    {
        if (!world.IsAlive(employee.Company) || !world.Has<CompanyAccount>(employee.Company))
            return;

        var account = world.Get<CompanyAccount>(employee.Company);
        if (account.State is CompanySolvency.Insolvent or CompanySolvency.Bankrupt)
            return;
        if (!world.IsAlive(employee.Office) || !world.Has<Office>(employee.Office) ||
            world.Get<Office>(employee.Office).IsClosed)
            return;

        Entity postingEntity = Entity.Null;
        JobPosting posting = default;
        world.Query(in new QueryDescription().WithAll<JobPosting>(),
            (Entity entity, ref JobPosting candidate) =>
            {
                if (postingEntity != Entity.Null)
                    return;
                if (candidate.Company != employee.Company ||
                    candidate.Office != employee.Office ||
                    !string.Equals(candidate.Position, employee.Position, StringComparison.Ordinal))
                    return;
                postingEntity = entity;
                posting = candidate;
            });

        if (postingEntity == Entity.Null)
            return;

        world.Create(new OpenPosition(postingEntity));
        posting.OpenSeats++;
        posting.TotalSeats++;
        posting.IsClosed = false;
        world.Get<JobPosting>(postingEntity) = posting;
        economy?.Emit(new JobSeatReserved(
            world.Has<NetworkId>(postingEntity) ? world.Get<NetworkId>(postingEntity).Value : 0UL,
            1));
    }

    public static void RemoveJobSearch(World world, Entity sim)
    {
        if (world.IsAlive(sim) && world.Has<JobSeeker>(sim))
            world.Remove<JobSeeker>(sim);
    }

    public static void EnsureWallet(World world, Entity sim, Money initial = default)
    {
        if (!world.IsAlive(sim) || world.Has<Wallet>(sim))
            return;
        world.Add(sim, new Wallet(initial));
    }

    public static Entity HouseholdOf(World world, EconomyContext economy, Entity sim)
    {
        if (economy.Housing.HouseholdBySim.TryGetValue(sim, out var household))
            return household;
        if (world.IsAlive(sim) && world.Has<Components.Housing.HouseholdMember>(sim))
            return world.Get<Components.Housing.HouseholdMember>(sim).Household;
        return Entity.Null;
    }
}
