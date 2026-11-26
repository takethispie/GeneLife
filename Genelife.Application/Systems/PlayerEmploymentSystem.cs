using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Player;
using Genelife.Economy;
using Genelife.Messages.Events;

namespace Genelife.Application.Systems;

/// <summary>
/// Monthly player attendance reconciliation. The player has no autonomous
/// commute, so presence is recorded when they use their own office and is
/// converted into the same monthly shift counters NPCs produce.
/// </summary>
public sealed class PlayerEmploymentSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly int interval;

    public PlayerEmploymentSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
    }

    public string Name => "PlayerEmployment";
    public int Order => 410;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var terminated = new List<(Entity Genos, Entity Company, string Reason)>();
        var query = new QueryDescription()
            .WithAll<PlayerEmployment, Employee>()
            .WithNone<Dead>();

        world.Query(in query, (Entity genos, ref PlayerEmployment player, ref Employee employee) =>
        {
            if (!world.Has<EmploymentRecord>(genos))
                world.Add(genos, new EmploymentRecord(employee.HiredAtTick));

            var record = world.Get<EmploymentRecord>(genos);
            record.ShiftsExpected++;

            if (player.AttendedThisMonth)
            {
                record.ShiftsCompleted++;
                player.ConsecutiveMissedShifts = 0;
                player.LastAttendedTick = tick;
            }
            else
            {
                player.ConsecutiveMissedShifts++;
            }

            player.AttendedThisMonth = false;
            world.Get<EmploymentRecord>(genos) = record;
            world.Get<PlayerEmployment>(genos) = player;

            if (player.ConsecutiveMissedShifts >= 3)
                terminated.Add((genos, employee.Company, "attendance"));
        });

        foreach (var (genos, company, reason) in terminated)
        {
            var playerId = PlayerIdOf(world, genos);
            if (!world.IsAlive(genos) || !world.Has<Employee>(genos))
                continue;

            var networkId = NetworkIdOf(genos);
            var companyNetworkId = NetworkIdOf(company);
            LifeSystemHelpers.RemoveEmployment(world, economy.Employment, economy, genos, tick);
            if (world.Has<PlayerEmployment>(genos))
                world.Remove<PlayerEmployment>(genos);
            economy.Emit(new PlayerTerminated(playerId, networkId, companyNetworkId, reason, tick));
        }
    }

    private static string PlayerIdOf(World world, Entity genos) =>
        world.Has<Components.Gen.PlayerControlled>(genos)
            ? world.Get<Components.Gen.PlayerControlled>(genos).PlayerId
            : string.Empty;

    private ulong NetworkIdOf(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;
}
