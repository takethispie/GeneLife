namespace Genelife.Api.DTOs;

public sealed record PlayerOverviewStatus(
    int Players,
    int Active,
    int AwaitingContinuation,
    int Employed,
    int Housed,
    int PendingInvitations,
    long DeathsThisYear,
    long ContinuationsThisYear,
    long CommandsAppliedThisYear);

public sealed record PlayerEmploymentStatus(
    bool Employed,
    ulong CompanyId,
    ulong OfficeId,
    string Position,
    string Seniority,
    long SalaryCents,
    bool AtWork,
    int ConsecutiveMissedShifts,
    long LastAttendedTick);

public sealed record PlayerHousingStatus(
    bool Housed,
    ulong HouseholdId,
    ulong UnitId,
    ulong PropertyId,
    string? PropertyName,
    string? UnitNumber,
    string? Tier,
    long RentCents,
    long RentArrearsCents,
    long UtilityArrearsCents,
    bool LeaseActive,
    int FoodUnits,
    int HouseholdGoods);

public sealed record PlayerJobOfferStatus(
    Guid OfferId,
    ulong PostingId,
    string Position,
    long SalaryCents,
    long ExpiresTick);

public sealed record PlayerLeaseOfferStatus(
    Guid OfferId,
    ulong UnitId,
    ulong PropertyId,
    long RentCents,
    long ExpiresTick);

public sealed record PlayerInvitationStatus(
    Guid Id,
    ulong FromSimId,
    ulong ToSimId,
    string Kind,
    string Status,
    long ExpiresTick,
    long CreatedTick);

public sealed record PlayerActionAuditStatus(
    Guid CommandId,
    string Kind,
    string Status,
    string? Code,
    long Tick);

public sealed record PlayerLifeStatus(
    string PlayerId,
    ulong SimNetworkId,
    string? Name,
    int Age,
    float Health,
    float Hunger,
    float Energy,
    float Hygiene,
    float Bladder,
    long WalletCents,
    long SavingsCents,
    bool IsAlive,
    bool AwaitingContinuation,
    string ContinuationPolicy,
    bool Observer,
    int NutritionBalance,
    int ConsecutivePoorDays,
    ulong? PartnerSimId,
    string? PartnerName,
    int Children,
    PlayerEmploymentStatus Employment,
    PlayerHousingStatus Housing,
    PlayerJobOfferStatus? JobOffer,
    PlayerLeaseOfferStatus? LeaseOffer,
    IReadOnlyList<PlayerInvitationStatus> Invitations,
    IReadOnlyList<PlayerActionAuditStatus> RecentActions);

public sealed record PlayerLifePage(
    IReadOnlyList<PlayerLifeStatus> Items,
    int Total);

public sealed record PlayerMeStatus(
    PlayerLifeStatus Life,
    IReadOnlyList<PlayerInvitationStatus> PendingInvitations,
    IReadOnlyList<PlayerActionAuditStatus> RecentActions);
