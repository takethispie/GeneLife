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
        Entity genos,
        long tick,
        bool retire = false)
    {
        if (!world.IsAlive(genos) || !world.Has<Employee>(genos))
            return;

        var employee = world.Get<Employee>(genos);
        world.Remove<Employee>(genos);
        index.Remove(genos);

        if (world.Has<WorkSchedule>(genos))
            world.Remove<WorkSchedule>(genos);
        if (world.Has<EmploymentRecord>(genos))
            world.Remove<EmploymentRecord>(genos);
        if (world.Has<CurrentAction>(genos))
            world.Get<CurrentAction>(genos).Type = Enums.ActionType.Idle;

        RepostSeat(world, economy, employee, tick);
        OfficeSeatAccounting.RefreshUsedSeats(world, employee.Office);

        if (retire)
        {
            economy?.Emit(new EmployeeRetired(
                NetworkIdOf(world, genos),
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

    public static void RemoveJobSearch(World world, Entity genos)
    {
        if (world.IsAlive(genos) && world.Has<JobSeeker>(genos))
            world.Remove<JobSeeker>(genos);
    }

    public static void EnsureWallet(World world, Entity genos, Money initial = default)
    {
        if (!world.IsAlive(genos) || world.Has<Wallet>(genos))
            return;
        world.Add(genos, new Wallet(initial));
    }

    public static Entity HouseholdOf(World world, EconomyContext economy, Entity genos)
    {
        if (economy.Housing.HouseholdByGenos.TryGetValue(genos, out var household))
            return household;
        if (world.IsAlive(genos) && world.Has<Components.Housing.HouseholdMember>(genos))
            return world.Get<Components.Housing.HouseholdMember>(genos).Household;
        return Entity.Null;
    }
}
