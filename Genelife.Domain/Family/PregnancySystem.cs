using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Housing;
using Genelife.Messages.Events;
using Genelife.Systems;
using Genelife.Values;

namespace Genelife.Family;

/// <summary>
/// Monthly pregnancy progression. Prenatal care is a household-to-government
/// transfer; unpaid care becomes arrears and raises risk, never negative money.
/// </summary>
public sealed class PregnancySystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly PregnancyIndex pregnancies;
    private readonly PregnancyService service;
    private readonly ReproductionOptions options;

    private readonly int cadenceOffset;

    public PregnancySystem(
        World world,
        EconomyContext economy,
        PregnancyIndex pregnancies,
        PregnancyService service,
        ReproductionOptions options,
        int cadenceOffset = 0)
    {
        this.world = world;
        this.economy = economy;
        this.pregnancies = pregnancies;
        this.service = service;
        this.options = options;
        this.cadenceOffset = cadenceOffset;
    }

    public string Name => "Pregnancy";
    public int Order => 375;
    public Cadence Cadence => new(Math.Max(1, economy.Clock.TicksPerMonth), cadenceOffset);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        options.Validate();

        var active = new List<Entity>();
        world.Query(in new QueryDescription().WithAll<Pregnancy>(),
            (Entity entity, ref Pregnancy pregnancy) =>
            {
                if (pregnancy.Status is PregnancyStatus.Active or PregnancyStatus.ReadyToBirth)
                    active.Add(entity);
            });
        active.Sort((a, b) => NetworkIdOf(a).CompareTo(NetworkIdOf(b)));

        foreach (var entity in active)
        {
            if (!world.IsAlive(entity) || !service.TryRead(entity, out var data))
                continue;

            if (!world.IsAlive(data.CarryingParent) || world.Has<Dead>(data.CarryingParent) ||
                !world.IsAlive(data.OtherParent) || world.Has<Dead>(data.OtherParent))
            {
                service.Cancel(entity, tick);
                economy.Emit(new PregnancyLost(NetworkIdOf(entity), "parents-unavailable", tick));
                continue;
            }

            if (data.Status == PregnancyStatus.ReadyToBirth)
                continue;

            if (tick >= data.DueTick)
            {
                service.SetReady(entity, tick);
                continue;
            }

            var household = Parenting.HouseholdOf(world, economy, data.CarryingParent);
            var cost = options.MonthlyPrenatalCareCost;
            var paid = Money.Zero;
            var shortfall = cost;
            if (household != Entity.Null && cost.IsPositive)
            {
                var finance = new HouseholdFinanceService(world, economy);
                finance.TryDebit(
                    household,
                    cost,
                    LedgerKind.MedicalCare,
                    tick,
                    EconomyAccountIds.Government,
                    "pregnancy.prenatal",
                    out shortfall);
                paid = cost - shortfall;
                if (paid.IsPositive)
                    economy.Government.Balance += paid;
            }

            var age = world.Has<Alive>(data.CarryingParent) ? world.Get<Alive>(data.CarryingParent).Age : 0;
            var health = world.Has<Health>(data.CarryingParent)
                ? (int)Math.Round(world.Get<Health>(data.CarryingParent).Value)
                : 100;
            var missedCare = shortfall.IsPositive;
            var risk = PregnancyRisk.Compute(
                age,
                health,
                data.MissedCareMonths + (missedCare ? 1 : 0),
                data.Complications,
                options);

            service.RecordCare(
                entity,
                paid,
                shortfall,
                missedCare,
                data.Complications,
                tick);
            service.Progress(entity, tick, risk);

            var monthKey = $"{economy.Clock.YearOf(tick)}.{economy.Clock.MonthOfYear(tick)}";
            var lossRoll = economy.Random
                .For($"reproduction.risk.{NetworkIdOf(entity)}.{monthKey}")
                .NextDouble();
            if (lossRoll < (double)PregnancyRisk.LossChance(risk, options))
            {
                service.Lose(entity, tick);
                economy.Emit(new PregnancyLost(NetworkIdOf(entity), "risk", tick));
                continue;
            }

            var monthsRemaining = Math.Max(
                0,
                (int)Math.Ceiling((data.DueTick - tick) / (double)economy.Clock.TicksPerMonth));
            economy.Emit(new PregnancyProgressed(
                NetworkIdOf(entity),
                risk,
                monthsRemaining,
                tick));
        }
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}

internal static class Parenting
{
    public static Entity HouseholdOf(World world, EconomyContext economy, Entity sim)
    {
        if (sim == Entity.Null || !world.IsAlive(sim))
            return Entity.Null;
        return economy.Housing.HouseholdOf(sim);
    }
}
