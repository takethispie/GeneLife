namespace Genelife.Api.DTOs;

/// <summary>
/// Stable cause codes. The server sends facts only; the client owns every word
/// of phrasing (GDD ch.08 §8.5(2)). Codes are part of the contract: add a new
/// constant rather than reusing an existing code for a different meaning.
/// </summary>
public static class CauseCodes
{
    public const string NeedHunger = "need_hunger";
    public const string NeedEnergy = "need_energy";
    public const string NeedHygiene = "need_hygiene";
    public const string NeedBladder = "need_bladder";
    public const string LastMeal = "last_meal";
    public const string NutritionBalance = "nutrition_balance";
    public const string NutritionPoorDays = "nutrition_poor_days";
    public const string WalletBalance = "wallet_balance";
    public const string SavingsBalance = "savings_balance";
    public const string SavingsInterest = "savings_interest";
    public const string LedgerEntry = "ledger_entry";
    public const string HousingTenure = "housing_tenure";
    public const string HousingTier = "housing_tier";
    public const string HousingHomeless = "housing_homeless";
    public const string MonthlyRent = "monthly_rent";
    public const string RentArrears = "rent_arrears";
    public const string ArrearsCycles = "arrears_cycles";
    public const string UtilityArrears = "utility_arrears";
    public const string EvictionNotice = "eviction_notice";
    public const string EmploymentPosition = "employment_position";
    public const string EmploymentSeniority = "employment_seniority";
    public const string EmploymentTenure = "employment_tenure";
    public const string SalaryPerCycle = "salary_per_cycle";
    public const string TopSkill = "top_skill";
    public const string UnmetDemand = "unmet_demand";
}

/// <summary>
/// One structured cause fact for a measure on the play surface.
/// </summary>
/// <param name="Code">Stable code from <see cref="CauseCodes"/>; the client maps it to phrasing.</param>
/// <param name="Subject">
/// Stable domain token identifying what the fact is about. For measures this is the
/// area (<c>needs</c>, <c>money</c>, <c>housing</c>, <c>work</c>); for a ledger entry it is
/// the ledger kind; for a label-valued fact (job position, top skill) it is the label
/// itself. Never prose, never a sentence.
/// </param>
/// <param name="Value">The numeric value of the fact, in <paramref name="Unit"/>.</param>
/// <param name="Unit">Stable unit token (<c>percent</c>, <c>cents</c>, <c>ticks</c>, ...).</param>
/// <param name="SinceTick">
/// The tick the fact has held since: the event tick for event-derived facts (last meal,
/// last ledger entry, move-in, hire), or the observation tick for a measure read as-of now.
/// </param>
public sealed record CauseFact(
    string Code,
    string Subject,
    double Value,
    string Unit,
    long SinceTick);

/// <summary>
/// The structured cause payload for one genos. The server states facts; the client
/// phrases them. A measure with no derivable cause is absent rather than invented.
/// </summary>
public sealed record GenosCausesStatus(
    ulong NetworkId,
    string Name,
    long AsOfTick,
    IReadOnlyList<CauseFact> Facts);
