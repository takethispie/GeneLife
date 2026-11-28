using Arch.Core;
using Genelife.Api.DTOs;
using Genelife.Components.Consumption;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.Application.Causes;

/// <summary>
/// Builds the structured cause facts for one genos (GDD ch.08 §8.5(2)).
///
/// The server states FACTS — code, subject, value, unit, sinceTick — and the client
/// owns the phrasing. Nothing here produces prose. A measure whose cause the
/// simulation cannot honestly state is emitted as its raw value with the observation
/// tick as <c>sinceTick</c>; it is never given an invented explanation.
/// </summary>
public static class CauseReadModel
{
    public static IReadOnlyList<CauseFact> Build(
        World world,
        Entity entity,
        long tick,
        FinanceLedger? ledger)
    {
        var facts = new List<CauseFact>(24);

        AddNeeds(world, entity, tick, facts);
        AddNutrition(world, entity, tick, facts);
        AddMoney(world, entity, tick, ledger, facts);
        AddHousing(world, entity, tick, facts);
        AddWork(world, entity, tick, facts);
        AddConsumption(world, entity, tick, facts);

        return facts;
    }

    private static void AddNeeds(World world, Entity entity, long tick, List<CauseFact> facts)
    {
        if (!world.TryGet<Needs>(entity, out var needs))
            return;

        facts.Add(new CauseFact(CauseCodes.NeedHunger, "needs", Round(needs.Hunger), "percent", tick));
        facts.Add(new CauseFact(CauseCodes.NeedEnergy, "needs", Round(needs.Energy), "percent", tick));
        facts.Add(new CauseFact(CauseCodes.NeedHygiene, "needs", Round(needs.Hygiene), "percent", tick));
        facts.Add(new CauseFact(CauseCodes.NeedBladder, "needs", Round(needs.Bladder), "percent", tick));
    }

    private static void AddNutrition(World world, Entity entity, long tick, List<CauseFact> facts)
    {
        if (!world.TryGet<NutritionState>(entity, out var nutrition))
            return;

        // The one event-derived nutrition cause: when the genos last ate. Rendering
        // "last meal 11 h ago" is the client's job, against the simulation clock.
        if (nutrition.LastMealTick > 0)
        {
            facts.Add(new CauseFact(
                CauseCodes.LastMeal,
                "nutrition",
                Math.Max(0, tick - nutrition.LastMealTick),
                "ticks",
                nutrition.LastMealTick));
        }

        facts.Add(new CauseFact(CauseCodes.NutritionBalance, "nutrition", nutrition.Balance, "points", tick));
        facts.Add(new CauseFact(CauseCodes.NutritionPoorDays, "nutrition", nutrition.ConsecutivePoorDays, "days", tick));
    }

    private static void AddMoney(
        World world,
        Entity entity,
        long tick,
        FinanceLedger? ledger,
        List<CauseFact> facts)
    {
        var networkId = NetworkIdOf(world, entity);

        if (world.TryGet<Wallet>(entity, out var wallet))
            facts.Add(new CauseFact(CauseCodes.WalletBalance, "money", Cents(wallet.Balance), "cents", tick));

        if (world.TryGet<SavingsAccount>(entity, out var savings))
        {
            facts.Add(new CauseFact(CauseCodes.SavingsBalance, "money", Cents(savings.Balance), "cents", tick));
            facts.Add(new CauseFact(
                CauseCodes.SavingsInterest,
                "money",
                Cents(savings.InterestEarned),
                "cents",
                savings.LastInterestTick));
        }

        if (ledger is null || networkId is null)
            return;

        // The ledger is the reason money moved. The most recent entry naming this
        // genos is the cause line for the balance, and its kind is the subject token.
        var recent = ledger.Recent(take: 1, entityId: networkId);
        if (recent.Count == 0)
            return;

        var entry = recent[0];
        var signedCents = entry.ToNetworkId == networkId ? entry.Amount.Cents : -entry.Amount.Cents;
        facts.Add(new CauseFact(
            CauseCodes.LedgerEntry,
            entry.Kind.ToString(),
            signedCents,
            "cents",
            entry.Tick));
    }

    private static void AddHousing(World world, Entity entity, long tick, List<CauseFact> facts)
    {
        if (!world.TryGet<Residence>(entity, out var residence) ||
            residence.Household == Entity.Null ||
            !world.TryGet<Household>(residence.Household, out var household))
        {
            // Homelessness is a raw fact, not a cause: the client shows it and
            // says nothing more.
            facts.Add(new CauseFact(CauseCodes.HousingHomeless, "housing", 1, "flag", tick));
            return;
        }

        facts.Add(new CauseFact(
            CauseCodes.HousingTenure,
            "housing",
            (int)household.Tenure,
            "tenure",
            household.MoveInTick));
        facts.Add(new CauseFact(
            CauseCodes.MonthlyRent,
            "housing",
            Cents(household.MonthlyRent),
            "cents",
            household.MoveInTick));
        facts.Add(new CauseFact(
            CauseCodes.RentArrears,
            "housing",
            Cents(household.RentArrears),
            "cents",
            household.LastRentTick));
        facts.Add(new CauseFact(
            CauseCodes.ArrearsCycles,
            "housing",
            household.ArrearsCycles,
            "cycles",
            household.LastRentTick));
        facts.Add(new CauseFact(
            CauseCodes.UtilityArrears,
            "housing",
            Cents(household.UtilityArrears),
            "cents",
            household.LastUtilityTick));

        if (household.NoticeStage > 0)
        {
            facts.Add(new CauseFact(
                CauseCodes.EvictionNotice,
                "housing",
                household.NoticeStage,
                "steps",
                household.NoticeStartedTick));
        }

        if (world.TryGet<HousingUnit>(residence.Unit, out var unit))
        {
            facts.Add(new CauseFact(
                CauseCodes.HousingTier,
                "housing",
                (int)unit.Tier,
                "tier",
                household.MoveInTick));
        }
    }

    private static void AddWork(World world, Entity entity, long tick, List<CauseFact> facts)
    {
        if (!world.TryGet<Employee>(entity, out var employee))
            return;

        facts.Add(new CauseFact(
            CauseCodes.EmploymentPosition,
            Blank(employee.Position) ?? "work",
            0,
            "label",
            employee.HiredAtTick));
        facts.Add(new CauseFact(
            CauseCodes.EmploymentSeniority,
            "work",
            (int)employee.Seniority,
            "seniority",
            employee.HiredAtTick));
        facts.Add(new CauseFact(
            CauseCodes.SalaryPerCycle,
            "work",
            Cents(employee.SalaryPerCycle),
            "cents",
            employee.HiredAtTick));

        if (world.TryGet<EmploymentRecord>(entity, out var record))
        {
            facts.Add(new CauseFact(
                CauseCodes.EmploymentTenure,
                "work",
                record.TenureCycles,
                "cycles",
                record.HiredAtTick));
        }

        if (world.TryGet<SkillProfile>(entity, out var profile))
        {
            var top = TopSkill(profile);
            if (top is not null)
            {
                facts.Add(new CauseFact(
                    CauseCodes.TopSkill,
                    top.Value.Skill.ToString(),
                    Round(top.Value.Proficiency),
                    "ratio",
                    tick));
            }
        }
    }

    private static void AddConsumption(World world, Entity entity, long tick, List<CauseFact> facts)
    {
        if (!world.TryGet<ConsumptionState>(entity, out var consumption))
            return;

        facts.Add(new CauseFact(
            CauseCodes.UnmetDemand,
            "consumption",
            Cents(consumption.UnmetDemand),
            "cents",
            tick));
    }

    private static (Skill Skill, float Proficiency)? TopSkill(in SkillProfile profile)
    {
        Skill? best = null;
        var bestValue = 0f;

        foreach (var skill in SkillProfile.Families)
        {
            var proficiency = profile.Get(skill);
            if (proficiency > bestValue)
            {
                bestValue = proficiency;
                best = skill;
            }
        }

        return best is null ? null : (best.Value, bestValue);
    }

    private static ulong? NetworkIdOf(World world, Entity entity) =>
        world.TryGet<NetworkId>(entity, out var networkId) ? networkId.Value : null;

    private static double Cents(Money money) => money.Cents;

    private static double Round(float value) => Math.Round(value, 1);

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
