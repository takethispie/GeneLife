using System.Text.Json;
using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.Commands;
using Genelife.Api.Repositories;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Family;
using Genelife.Components.Finance;
using Genelife.Components.Consumption;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Components.Map;
using Genelife.Components.Player;
using Genelife.Consumption;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Housing;
using Genelife.Messages.Events;
using Genelife.Map;
using Genelife.Player;
using Genelife.Social;
using Genelife.Application.Systems;
using Genelife.Values;

namespace Genelife.Api.Services;

public readonly record struct PlayerActionResult(
    bool Applied,
    string? Code,
    string? Message,
    object? Data = null)
{
    public static PlayerActionResult Success(string? message = null, object? data = null) =>
        new(true, null, message, data);

    /// <summary>Refuses with a member of the closed player-action refusal set.</summary>
    public static PlayerActionResult Reject(PlayerRefusal refusal, string message) =>
        new(false, refusal.WireCode(), message);

    /// <summary>
    /// Refuses with a code the closed set does not name. The enum covers the action path only, so
    /// eligibility, relationship and venue results still hand back a raw string.
    /// </summary>
    [Obsolete("Prefer Reject(PlayerRefusal, string). Kept for the rest of the Application layer while it still produces raw codes.")]
    public static PlayerActionResult Reject(string code, string message) =>
        new(false, code, message);
}

/// <summary>
/// Applies typed player actions on the tick thread. Every action validates
/// ownership and proximity, then reuses the same domain services and ledger
/// paths as NPC systems. No action mutates the world outside the tick drain.
/// </summary>
public sealed partial class PlayerActionService
{
    private const int OfferLifetimeTicks = 100;
    private const int InvitationLifetimeTicks = 100;
    private const int ConsentCooldownTicks = 600;

    private readonly World world;
    private readonly EconomyContext economy;
    private readonly NetworkIdRegistry networkIds;
    private readonly PlayerRegistry players;
    private readonly RelationshipIndex relationships;
    private readonly RelationshipService relationshipService;
    private readonly PregnancyIndex pregnancies;
    private readonly PregnancyService pregnancyService;
    private readonly RelationshipOptions relationshipOptions;
    private readonly ReproductionOptions reproductionOptions;
    private readonly HouseholdRepository households;
    private readonly HousingAllocator allocator;
    private readonly HouseholdFinanceService finance;
    private readonly HiringSystem hiring;
    private readonly GroceryStoreService groceryStores;
    private readonly PlayerRelationshipService playerRelationships;
    private readonly VenueService venues;
    private readonly VenueActivityService venueActivities;
    private readonly WorldMap? map;

    public PlayerActionService(
        World world,
        EconomyContext economy,
        NetworkIdRegistry networkIds,
        PlayerRegistry players,
        RelationshipIndex relationships,
        RelationshipService relationshipService,
        PregnancyIndex pregnancies,
        PregnancyService pregnancyService,
        RelationshipOptions relationshipOptions,
        ReproductionOptions reproductionOptions,
        PlayerRelationshipService? playerRelationships = null,
        VenueService? venues = null,
        VenueActivityService? venueActivities = null,
        WorldMap? map = null)
    {
        this.world = world;
        this.economy = economy;
        this.networkIds = networkIds;
        this.players = players;
        this.relationships = relationships;
        this.relationshipService = relationshipService;
        this.pregnancies = pregnancies;
        this.pregnancyService = pregnancyService;
        this.relationshipOptions = relationshipOptions;
        this.reproductionOptions = reproductionOptions;
        households = new HouseholdRepository(world, networkIds, economy);
        allocator = new HousingAllocator(world, economy);
        finance = new HouseholdFinanceService(world, economy);
        hiring = new HiringSystem(world, economy.Employment, economy: economy);
        groceryStores = new GroceryStoreService(world, economy, entity => networkIds.AllocateInfrastructure(entity));
        var fallbackGraph = new SocialGraphService();
        this.playerRelationships = playerRelationships ?? new PlayerRelationshipService(
            world,
            economy,
            players,
            relationships,
            relationshipService,
            fallbackGraph,
            relationshipOptions);
        this.venues = venues ?? new VenueService(world, players, economy);
        this.venueActivities = venueActivities ?? new VenueActivityService(
            world,
            economy,
            players,
            this.venues,
            fallbackGraph);
        this.map = map;
    }

    public PlayerActionResult Apply(
        string playerId,
        Entity genos,
        PlayerActionKind kind,
        JsonElement payload,
        long tick)
    {
        if (world is null || !world.IsAlive(genos))
            return PlayerActionResult.Reject(PlayerRefusal.GenosNotFound, "Your genos is not available.");
        if (world.Has<Dead>(genos))
            return PlayerActionResult.Reject(PlayerRefusal.GenosNotFound, "Your genos is dead.");

        var owner = world.Has<PlayerControlled>(genos)
            ? world.Get<PlayerControlled>(genos).PlayerId
            : null;
        if (!string.Equals(owner, playerId, StringComparison.Ordinal))
            return PlayerActionResult.Reject(PlayerRefusal.NotOwner, "You do not control that genos.");

        // The catalogue read model calls this SAME evaluation (GDD chapter 08 §8.5(1), AC-4a); the
        // handlers below only mutate, using the facts the evaluation resolved.
        var evaluation = Evaluate(genos, kind, payload, tick);
        if (!evaluation.Allowed)
            return evaluation.ToActionResult();

        return ApplyEvaluation(playerId, genos, kind, evaluation, tick);
    }

    private PlayerActionResult ApplyEvaluation(
        string playerId,
        Entity genos,
        PlayerActionKind kind,
        ActionEvaluation evaluation,
        long tick) =>
        kind switch
        {
            PlayerActionKind.ApplyForJob => ApplyForJob(genos, evaluation, tick),
            PlayerActionKind.AcceptJobOffer => ApplyJobOffer(genos, evaluation, tick),
            PlayerActionKind.QuitJob => ApplyQuitJob(genos, evaluation, tick),
            PlayerActionKind.RequestHousing => ApplyRequestHousing(genos, evaluation, tick),
            PlayerActionKind.AcceptLease => ApplyAcceptLease(playerId, genos, evaluation, tick),
            PlayerActionKind.MoveOut => ApplyMoveOut(genos, evaluation, tick),
            PlayerActionKind.PayArrears => ApplyPayArrears(genos, evaluation, tick),
            PlayerActionKind.BuyItem => ApplyBuyItem(genos, evaluation, tick),
            PlayerActionKind.ConsumeItem => ApplyConsumeItem(genos, evaluation, tick),
            PlayerActionKind.InvitePartner => ApplyInvitePartner(playerId, genos, evaluation, tick),
            PlayerActionKind.InviteCohabit => ApplyInviteCohabit(playerId, genos, evaluation, tick),
            PlayerActionKind.EndPartnership => ApplyEndPartnership(playerId, genos, evaluation, tick),
            PlayerActionKind.PerformVenueActivity => ApplyPerformVenueActivity(playerId, genos, evaluation, tick),
            PlayerActionKind.RespondToInvitation => ApplyRespondToInvitation(playerId, genos, evaluation, tick),
            PlayerActionKind.TryForChild => ApplyTryForChild(genos, evaluation, tick),
            PlayerActionKind.NameChild => ApplyNameChild(genos, evaluation, tick),
            PlayerActionKind.ChooseContinuation => ApplyChooseContinuation(playerId, genos, evaluation, tick),
            _ => evaluation.ToActionResult()
        };

    /// <summary>
    /// Marks monthly attendance when a player works at their own office. The
    /// employment reconciliation system converts this signal into payroll-shift
    /// counters at the month boundary.
    /// </summary>
    public void RecordWorkPresence(Entity genos, long tick)
    {
        if (!world.IsAlive(genos) || !world.Has<PlayerEmployment>(genos) || !world.Has<Employee>(genos))
            return;

        if (!world.Has<CurrentAction>(genos) || world.Get<CurrentAction>(genos).Type != ActionType.Working)
            return;

        var employment = world.Get<PlayerEmployment>(genos);
        var employee = world.Get<Employee>(genos);
        if (employment.Office != employee.Office)
            return;

        employment.AttendedThisMonth = true;
        employment.LastAttendedTick = tick;
        world.Get<PlayerEmployment>(genos) = employment;
    }

    private static PlayerActionResult MapRelationshipResult(PlayerRelationshipResult result)
    {
        if (!result.Applied)
            return RejectRuntimeCode(result.Code, result.Message, "Not available.");

        object? data = result.Data;
        if (result.Invitation is not null)
            data ??= new { invitationId = result.Invitation.Id, pendingConsent = true };
        return PlayerActionResult.Success(result.Message, data);
    }

    private bool AcceptPartnership(Entity first, Entity second, long tick, int score)
    {
        if (!relationshipService.TryCreatePair(
                first,
                second,
                RelationshipKind.Partner,
                RelationshipSource.Player,
                tick,
                out _,
                out _,
                out _))
            return false;

        economy.Emit(new PartnershipFormed(NetworkIdOf(first), NetworkIdOf(second), score, tick));

        if (relationshipOptions.CohabitOnPairing &&
            new HouseholdMergeService(world, economy).TryMergePartners(
                first,
                second,
                out _,
                out _,
                out var fromHouseholdId,
                out var toHouseholdId) &&
            fromHouseholdId != 0 && toHouseholdId != 0 && fromHouseholdId != toHouseholdId)
        {
            economy.Emit(new HouseholdMerged(fromHouseholdId, toHouseholdId, tick));
        }

        return true;
    }

    /// <summary>
    /// Apply phase of conception. The preconditions (carrying parent, CanConceive) come from
    /// <c>Evaluate</c>; what remains here is the monthly roll and the pregnancy itself.
    /// </summary>
    private PlayerActionResult TryConceiveAfterEvaluation(Entity first, Entity second, long tick)
    {
        var carrying = first;
        var other = second;

        var firstId = NetworkIdOf(first);
        var secondId = NetworkIdOf(second);
        var key = firstId <= secondId ? (firstId, secondId) : (secondId, firstId);
        var stream = $"reproduction.conception.{key.Item1}.{key.Item2}." +
                     $"{economy.Clock.YearOf(tick)}.{economy.Clock.MonthOfYear(tick)}";
        if (economy.Random.For(stream).NextDouble() >= (double)reproductionOptions.MonthlyConceptionChance)
            return PlayerActionResult.Reject(PlayerRefusal.NotConceived, "No pregnancy occurred this month.");

        var health = world.Has<Health>(carrying)
            ? (int)Math.Round(world.Get<Health>(carrying).Value)
            : 100;
        var due = tick + (long)reproductionOptions.PregnancyMonths * economy.Clock.TicksPerMonth;
        if (!pregnancyService.TryStart(
                carrying,
                other,
                tick,
                due,
                health,
                PregnancySource.Conception,
                reproductionOptions,
                out var pregnancy,
                out var pregnancyError))
            return PlayerActionResult.Reject(PlayerRefusal.InvalidState, pregnancyError.ToString());

        var pregnancyId = NetworkIdOf(pregnancy);
        economy.Emit(new PregnancyStarted(
            pregnancyId,
            NetworkIdOf(carrying),
            NetworkIdOf(other),
            tick,
            due,
            tick));
        economy.Emit(new ConceptionOccurred(
            NetworkIdOf(carrying),
            NetworkIdOf(other),
            pregnancyId,
            tick));
        return PlayerActionResult.Success("A pregnancy has started.", new { pregnancyId, dueTick = due });
    }

    private Entity ChooseCarryingParent(Entity first, Entity second)
    {
        if (!world.Has<Traits>(first) || !world.Has<Traits>(second))
            return Entity.Null;
        var firstTraits = world.Get<Traits>(first);
        var secondTraits = world.Get<Traits>(second);
        if (firstTraits.Sex != secondTraits.Sex)
            return firstTraits.Sex == Sex.Female ? first : second;
        if (!reproductionOptions.AllowSameSexCarryingPair)
            return Entity.Null;
        return NetworkIdOf(first) <= NetworkIdOf(second) ? first : second;
    }

    private bool CanConceive(Entity carrying, Entity other, long tick, out string reason)
    {
        reason = string.Empty;
        if (world.Has<Dead>(carrying) || world.Has<Dead>(other))
        {
            reason = "Both parents must be alive.";
            return false;
        }
        if (PregnancyService.IsDependent(world, carrying) || PregnancyService.IsDependent(world, other))
        {
            reason = "Dependants cannot conceive.";
            return false;
        }
        if (pregnancies.TryGetActive(carrying, out _))
        {
            reason = "There is already an active pregnancy.";
            return false;
        }
        if (pregnancies.IsInLossCooldown(carrying, tick, reproductionOptions, economy.Clock))
        {
            reason = "The carrying parent is in a loss cooldown.";
            return false;
        }

        var age = world.Has<Alive>(carrying) ? world.Get<Alive>(carrying).Age : 0;
        if (age < reproductionOptions.MinimumConceptionAge || age > reproductionOptions.MaximumConceptionAge)
        {
            reason = "The carrying parent is outside the conception age range.";
            return false;
        }
        var otherAge = world.Has<Alive>(other) ? world.Get<Alive>(other).Age : 0;
        if (otherAge < reproductionOptions.MinimumConceptionAge)
        {
            reason = "Both parents must be adults.";
            return false;
        }

        var health = world.Has<Health>(carrying) ? world.Get<Health>(carrying).Value : 100f;
        if (health < reproductionOptions.MinimumConceptionHealth)
        {
            reason = "The carrying parent is not healthy enough.";
            return false;
        }

        return true;
    }

    private readonly record struct ConsentResult(bool Accepted, int Score, string? Code, string Message);

    private ConsentResult EvaluateNpcConsent(Entity from, Entity target, long tick)
    {
        if (!world.Has<Traits>(from) || !world.Has<Traits>(target))
            return new ConsentResult(false, 0, "missing-traits", "Both genoses need a trait set.");

        var health = world.Has<Health>(target)
            ? (int)Math.Clamp(world.Get<Health>(target).Value * 100f, 0f, 10_000f)
            : 10_000;
        var score = AttractionScorer.Score(
            world.Get<Traits>(from),
            world.Get<Alive>(from).Age,
            world.Get<Traits>(target),
            world.Get<Alive>(target).Age,
            proximityBasisPoints: 10_000,
            stabilityBasisPoints: 5_000,
            healthBasisPoints: health,
            bothAvailable: !relationships.HasPartner(from) && !relationships.HasPartner(target));

        if (score.Total < relationshipOptions.PairingThreshold)
            return new ConsentResult(false, score.Total, "not-attracted", "The invitation was declined.");

        var chance = (double)relationshipOptions.PairingAcceptanceChance * score.Total / AttractionScorer.ScoreScale;
        var roll = economy.Random
            .For($"social.encounter.{NetworkIdOf(from)}.{NetworkIdOf(target)}.{tick}")
            .NextDouble();
        if (roll >= chance)
            return new ConsentResult(false, score.Total, "declined", "The invitation was declined.");

        return new ConsentResult(true, score.Total, null, "Accepted.");
    }

    private Entity CreateInvitation(Entity from, Entity to, FamilyInvitationKind kind, long tick)
    {
        var entity = world.Create(new FamilyInvitation(
            Guid.NewGuid(),
            from,
            to,
            kind,
            tick,
            tick + InvitationLifetimeTicks));
        var networkId = networkIds.Allocate(entity);
        world.Add(entity, networkId);
        return entity;
    }

    private bool HasPendingInvitation(Entity from, Entity to, FamilyInvitationKind? kind = null)
    {
        var found = false;
        world.Query(in new QueryDescription().WithAll<FamilyInvitation>(),
            (ref FamilyInvitation invitation) =>
            {
                if (found || invitation.Status != FamilyInvitationStatus.Pending)
                    return;
                if (invitation.From != from || invitation.To != to)
                    return;
                if (kind is not null && invitation.Kind != kind.Value)
                    return;
                found = true;
            });
        return found;
    }

    private bool TryFindInvitation(Guid id, out Entity entity)
    {
        var found = Entity.Null;
        world.Query(in new QueryDescription().WithAll<FamilyInvitation>(),
            (Entity candidate, ref FamilyInvitation invitation) =>
            {
                if (found != Entity.Null || invitation.Id != id)
                    return;
                found = candidate;
            });
        entity = found;
        return found != Entity.Null;
    }

    private bool InConsentCooldown(Entity target, Entity from, long tick) =>
        world.IsAlive(target) &&
        world.Has<FamilyConsentCooldown>(target) &&
        world.Get<FamilyConsentCooldown>(target).From == from &&
        tick < world.Get<FamilyConsentCooldown>(target).UntilTick;

    private void SetConsentCooldown(Entity target, Entity from, long tick)
    {
        if (!world.IsAlive(target) || world.Has<Dead>(target))
            return;
        var cooldown = new FamilyConsentCooldown { From = from, UntilTick = tick + ConsentCooldownTicks };
        if (world.Has<FamilyConsentCooldown>(target))
            world.Get<FamilyConsentCooldown>(target) = cooldown;
        else
            world.Add(target, cooldown);
    }

    private PlayerActionResult Malformed(string? error) =>
        PlayerActionResult.Reject(PlayerRefusal.MalformedPayload, error ?? "The payload could not be read.");

    /// <summary>
    /// Passes through a refusal code produced outside the closed action-path set: an eligibility,
    /// relationship or venue result still carries a raw string (GDD chapter 07 §7.6 maps those
    /// consumer-side, by wire string). The closed set names the action path's own vocabulary, so
    /// these three call sites are the only ones allowed to bypass it, and only through here.
    /// </summary>
#pragma warning disable CS0618 // the string overload is the deliberate escape hatch for these producers
    private static PlayerActionResult RejectRuntimeCode(string? code, string? message, string fallbackMessage) =>
        PlayerActionResult.Reject(code ?? PlayerRefusal.InvalidState.WireCode(), message ?? fallbackMessage);
#pragma warning restore CS0618

    private bool TryEntity(ulong networkId, out Entity entity)
    {
        entity = Entity.Null;
        return networkId != 0 &&
               networkIds.TryGetEntity(new NetworkId(networkId), out entity) &&
               world.IsAlive(entity);
    }

    private string PlayerIdOf(Entity genos) =>
        world.IsAlive(genos) && world.Has<PlayerControlled>(genos)
            ? world.Get<PlayerControlled>(genos).PlayerId
            : string.Empty;

    private ulong NetworkIdOf(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;

    private bool Near(Entity genos, Entity building)
    {
        if (!world.IsAlive(genos) || !world.Has<TilePosition>(genos) || !world.Has<Placement>(building))
            return true;

        var entry = world.Get<Placement>(building).Entry;
        return world.Get<TilePosition>(genos).Tile.ChebyshevDistanceTo(entry) <= 1;
    }

    private bool TryFirstOpenPosting(Entity office, out Entity postingEntity)
    {
        var candidates = new List<Entity>();
        world.Query(in new QueryDescription().WithAll<JobPosting>(),
            (Entity entity, ref JobPosting posting) =>
            {
                if (posting.Office == office && !posting.IsClosed && posting.OpenSeats > 0)
                    candidates.Add(entity);
            });
        candidates.Sort((a, b) => NetworkIdOf(a).CompareTo(NetworkIdOf(b)));
        postingEntity = candidates.Count > 0 ? candidates[0] : Entity.Null;
        return postingEntity != Entity.Null;
    }

    private bool MeetsRequirements(Entity genos, in JobPosting posting)
    {
        if (posting.Requirements == Skill.None)
            return true;
        var profile = world.Has<SkillProfile>(genos) ? world.Get<SkillProfile>(genos) : new SkillProfile();
        return (profile.ToMask() & posting.Requirements) != Skill.None;
    }

    private bool CompanyHiringAllowed(Entity company, out string error)
    {
        error = string.Empty;
        if (!economy.Accounts.TryGet(company, out var account))
        {
            error = "The company is not accepting applications.";
            return false;
        }
        if (account.State is CompanySolvency.Insolvent or CompanySolvency.Bankrupt)
        {
            error = "The company is not hiring.";
            return false;
        }
        if (account.HiringFrozen)
        {
            error = "The company has frozen hiring.";
            return false;
        }
        if (account.Arrears.IsPositive)
        {
            error = "The company is not hiring while it has arrears.";
            return false;
        }
        return true;
    }

    private bool TryResolveVacantUnit(ulong unitNetworkId, ulong propertyNetworkId, out Entity unit)
    {
        unit = Entity.Null;
        if (unitNetworkId != 0)
        {
            if (!TryEntity(unitNetworkId, out unit) || !world.Has<HousingUnit>(unit))
                return false;
            return world.Get<HousingUnit>(unit).IsVacant;
        }

        if (propertyNetworkId == 0 || !TryEntity(propertyNetworkId, out var property))
            return false;

        var candidates = new List<Entity>();
        world.Query(in new QueryDescription().WithAll<HousingUnit>(),
            (Entity entity, ref HousingUnit data) =>
            {
                if (data.Property == property && data.IsVacant && data.Capacity >= 1)
                    candidates.Add(entity);
            });
        candidates.Sort((a, b) =>
        {
            var byRent = world.Get<HousingUnit>(a).BaseRent.Cents.CompareTo(world.Get<HousingUnit>(b).BaseRent.Cents);
            return byRent != 0 ? byRent : NetworkIdOf(a).CompareTo(NetworkIdOf(b));
        });
        unit = candidates.Count > 0 ? candidates[0] : Entity.Null;
        return unit != Entity.Null;
    }

    private bool TryDebitPlayer(
        Entity genos,
        Money amount,
        LedgerKind kind,
        long tick,
        string reference,
        ulong? payee = null)
    {
        if (!world.IsAlive(genos) || !amount.IsPositive)
            return false;

        var walletBalance = world.Has<Wallet>(genos) ? world.Get<Wallet>(genos).Balance : Money.Zero;
        var savingsBalance = world.Has<SavingsAccount>(genos)
            ? world.Get<SavingsAccount>(genos).Balance
            : Money.Zero;
        if (walletBalance + savingsBalance < amount)
            return false;

        var remaining = amount;
        if (walletBalance.IsPositive)
        {
            ref var wallet = ref world.Get<Wallet>(genos);
            var take = wallet.Balance.Min(remaining);
            wallet.Balance -= take;
            remaining -= take;
        }
        if (remaining.IsPositive && savingsBalance.IsPositive)
        {
            ref var savings = ref world.Get<SavingsAccount>(genos);
            var take = savings.Balance.Min(remaining);
            savings.Balance -= take;
            savings.LifetimeWithdrawn += take;
            remaining -= take;
        }

        economy.Ledger.Record(
            Guid.NewGuid(),
            tick,
            kind,
            NetworkIdOf(genos),
            payee,
            amount,
            reference);
        return true;
    }

    private void CreditLandlord(Entity household, Money amount, long tick)
    {
        if (!amount.IsPositive)
            return;

        var unit = economy.Housing.UnitOf(household);
        var landlord = unit != Entity.Null ? economy.Housing.EffectiveOwner(unit) : Entity.Null;
        if (landlord != Entity.Null && world.IsAlive(landlord) && world.Has<CompanyName>(landlord))
        {
            economy.Accounts.ApplySettlement(landlord, amount, tick, revenue: true);
            economy.RevenueThisYear += amount;
            return;
        }

        if (landlord != Entity.Null && world.IsAlive(landlord) && world.Has<Household>(landlord))
        {
            finance.CreditHousehold(landlord, amount, LedgerKind.RentArrears, tick, NetworkIdOf(household), "player.arrears");
            return;
        }

        economy.Government.Balance += amount;
    }

    private bool TryResolveStore(ulong preferred, Entity genos, out Entity store, out Entity seller)
    {
        store = Entity.Null;
        seller = Entity.Null;

        if (preferred != 0)
        {
            if (!TryEntity(preferred, out store))
                return false;
        }
        else
        {
            var candidates = new List<Entity>();
            world.Query(in new QueryDescription().WithAll<Office>().WithNone<Dead>(),
                (Entity entity, ref Office _) => candidates.Add(entity));
            candidates.Sort((a, b) =>
            {
                var aNear = Near(genos, a) ? 0 : 1;
                var bNear = Near(genos, b) ? 0 : 1;
                var byNear = aNear.CompareTo(bNear);
                return byNear != 0 ? byNear : NetworkIdOf(a).CompareTo(NetworkIdOf(b));
            });
            store = candidates.Count > 0 ? candidates[0] : Entity.Null;
            if (store == Entity.Null)
                return false;
        }

        seller = world.Has<Office>(store)
            ? world.Get<Office>(store).Company
            : world.Has<CompanyName>(store) ? store : Entity.Null;
        return true;
    }

    private void CreditSeller(Entity seller, Entity store, Money amount, string category, long tick)
    {
        if (!amount.IsPositive)
            return;

        if (seller != Entity.Null && world.IsAlive(seller) && world.Has<CompanyName>(seller))
        {
            // Pending revenue is settled by CompanyRevenueSystem on its schedule;
            // crediting the balance here as well would double-count the sale.
            economy.BookRevenue(seller, category, NetworkIdOf(store), amount, tick);
            return;
        }

        economy.Government.Balance += amount;
    }

    private Entity PantryOwner(Entity genos) =>
        LifeSystemHelpers.HouseholdOf(world, economy, genos) is { } household && household != Entity.Null
            ? household
            : genos;

    private void EnsurePantry(Entity owner)
    {
        if (owner == Entity.Null || !world.IsAlive(owner) || world.Has<HouseholdPantry>(owner))
            return;
        world.Add(owner, new HouseholdPantry());
    }

    private void StorePantry(Entity owner, HouseholdPantry pantry)
    {
        if (owner == Entity.Null || !world.IsAlive(owner))
            return;
        if (world.Has<HouseholdPantry>(owner))
            world.Get<HouseholdPantry>(owner) = pantry;
        else
            world.Add(owner, pantry);
    }
}
