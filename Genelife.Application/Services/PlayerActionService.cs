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

    public static PlayerActionResult Reject(string code, string message) =>
        new(false, code, message);
}

/// <summary>
/// Applies typed player actions on the tick thread. Every action validates
/// ownership and proximity, then reuses the same domain services and ledger
/// paths as NPC systems. No action mutates the world outside the tick drain.
/// </summary>
public sealed class PlayerActionService
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
        VenueActivityService? venueActivities = null)
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
    }

    public PlayerActionResult Apply(
        string playerId,
        Entity genos,
        PlayerActionKind kind,
        JsonElement payload,
        long tick)
    {
        if (world is null || !world.IsAlive(genos))
            return PlayerActionResult.Reject("genos_not_found", "Your genos is not available.");
        if (world.Has<Dead>(genos))
            return PlayerActionResult.Reject("genos_not_found", "Your genos is dead.");

        var owner = world.Has<PlayerControlled>(genos)
            ? world.Get<PlayerControlled>(genos).PlayerId
            : null;
        if (!string.Equals(owner, playerId, StringComparison.Ordinal))
            return PlayerActionResult.Reject("not_owner", "You do not control that genos.");

        return kind switch
        {
            PlayerActionKind.ApplyForJob => ApplyForJob(genos, payload, tick),
            PlayerActionKind.AcceptJobOffer => AcceptJobOffer(genos, payload, tick),
            PlayerActionKind.QuitJob => QuitJob(genos, payload, tick),
            PlayerActionKind.RequestHousing => RequestHousing(genos, payload, tick),
            PlayerActionKind.AcceptLease => AcceptLease(playerId, genos, payload, tick),
            PlayerActionKind.MoveOut => MoveOut(genos, payload, tick),
            PlayerActionKind.PayArrears => PayArrears(genos, payload, tick),
            PlayerActionKind.BuyItem => BuyItem(genos, payload, tick),
            PlayerActionKind.ConsumeItem => ConsumeItem(genos, payload, tick),
            PlayerActionKind.InvitePartner => InvitePartner(playerId, genos, payload, tick),
            PlayerActionKind.InviteCohabit => InviteCohabit(playerId, genos, payload, tick),
            PlayerActionKind.EndPartnership => EndPartnership(playerId, genos, payload, tick),
            PlayerActionKind.PerformVenueActivity => PerformVenueActivity(playerId, genos, payload, tick),
            PlayerActionKind.RespondToInvitation => RespondToInvitation(playerId, genos, payload, tick),
            PlayerActionKind.TryForChild => TryForChild(playerId, genos, payload, tick),
            PlayerActionKind.NameChild => NameChild(genos, payload, tick),
            PlayerActionKind.ChooseContinuation => ChooseContinuation(playerId, genos, payload, tick),
            _ => PlayerActionResult.Reject("invalid_state", "Unknown player action.")
        };
    }

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

    private PlayerActionResult ApplyForJob(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.ApplyForJob>(payload, out var request, out var error))
            return Malformed(error);

        if (!TryEntity(request!.OfficeNetworkId, out var office) || !world.Has<Office>(office))
            return PlayerActionResult.Reject("not_available", "That office is not available.");
        if (!Near(genos, office))
            return PlayerActionResult.Reject("out_of_range", "Walk to the office entrance first.");
        if (world.Has<Employee>(genos))
            return PlayerActionResult.Reject("already_employed", "You already have a job.");

        Entity postingEntity;
        if (request.PostingNetworkId != 0)
        {
            if (!TryEntity(request.PostingNetworkId, out postingEntity) ||
                !world.Has<JobPosting>(postingEntity))
                return PlayerActionResult.Reject("not_available", "That posting no longer exists.");
        }
        else if (!TryFirstOpenPosting(office, out postingEntity))
        {
            return PlayerActionResult.Reject("not_available", "There are no open postings at this office.");
        }

        var posting = world.Get<JobPosting>(postingEntity);
        if (posting.Office != office || posting.IsClosed || posting.OpenSeats <= 0)
            return PlayerActionResult.Reject("not_available", "That posting is not open.");
        if (world.Has<Lifecycle>(genos) && world.Get<Lifecycle>(genos).Stage != LifeStage.Work)
            return PlayerActionResult.Reject("invalid_state", "Only working-age genoses can apply.");
        if (!MeetsRequirements(genos, posting))
            return PlayerActionResult.Reject("not_available", "You do not meet the posting requirements.");
        if (!CompanyHiringAllowed(posting.Company, out var hiringError))
            return PlayerActionResult.Reject("not_available", hiringError);

        var offer = new PlayerJobOffer
        {
            OfferId = Guid.NewGuid(),
            Posting = postingEntity,
            Office = office,
            Position = posting.Position,
            SalaryPerCycle = posting.SalaryPerCycle,
            CreatedTick = tick,
            ExpiresTick = tick + OfferLifetimeTicks
        };

        if (world.Has<PlayerJobOffer>(genos))
            world.Get<PlayerJobOffer>(genos) = offer;
        else
            world.Add(genos, offer);

        return PlayerActionResult.Success("Job offer created.", new
        {
            offer.OfferId,
            offer.Position,
            SalaryCents = offer.SalaryPerCycle.Cents,
            offer.ExpiresTick
        });
    }

    private PlayerActionResult AcceptJobOffer(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.AcceptJobOffer>(payload, out var request, out var error))
            return Malformed(error);
        if (!world.Has<PlayerJobOffer>(genos))
            return PlayerActionResult.Reject("offer_not_found", "There is no job offer to accept.");

        var offer = world.Get<PlayerJobOffer>(genos);
        if (request!.OfferId != Guid.Empty && request.OfferId != offer.OfferId)
            return PlayerActionResult.Reject("offer_not_found", "That job offer is not yours.");
        if (tick > offer.ExpiresTick)
        {
            world.Remove<PlayerJobOffer>(genos);
            return PlayerActionResult.Reject("offer_expired", "The job offer has expired.");
        }

        if (!hiring.TryHireApplicant(genos, offer.Posting, tick, out var hireError))
            return PlayerActionResult.Reject("not_available", hireError ?? "The posting is no longer available.");

        world.Remove<PlayerJobOffer>(genos);
        if (!world.Has<PlayerEmployment>(genos))
            world.Add(genos, new PlayerEmployment(offer.Office, tick, PlayerEmploymentSource.PlayerAcceptedOffer));
        else
            world.Get<PlayerEmployment>(genos) = new PlayerEmployment(offer.Office, tick, PlayerEmploymentSource.PlayerAcceptedOffer);

        LifeSystemHelpers.EnsureWallet(world, genos);

        economy.Emit(new PlayerHired(
            PlayerIdOf(genos),
            NetworkIdOf(genos),
            NetworkIdOf(world.Get<Employee>(genos).Company),
            NetworkIdOf(offer.Office),
            offer.Position,
            offer.SalaryPerCycle.Cents,
            tick));

        return PlayerActionResult.Success("You accepted the job.", new
        {
            offer.Position,
            SalaryCents = offer.SalaryPerCycle.Cents
        });
    }

    private PlayerActionResult QuitJob(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.QuitJob>(payload, out var request, out var error))
            return Malformed(error);
        if (!world.Has<Employee>(genos))
            return PlayerActionResult.Reject("not_employed", "You do not have a job.");

        var employee = world.Get<Employee>(genos);
        var reason = string.IsNullOrWhiteSpace(request!.Reason) ? "player.quit" : request.Reason!.Trim();
        LifeSystemHelpers.RemoveEmployment(world, economy.Employment, economy, genos, tick);
        if (world.Has<PlayerEmployment>(genos))
            world.Remove<PlayerEmployment>(genos);
        if (world.Has<PlayerJobOffer>(genos))
            world.Remove<PlayerJobOffer>(genos);

        economy.Emit(new PlayerQuitJob(PlayerIdOf(genos), NetworkIdOf(genos), NetworkIdOf(employee.Company), reason, tick));
        return PlayerActionResult.Success("You quit your job.");
    }

    private PlayerActionResult RequestHousing(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.RequestHousing>(payload, out var request, out var error))
            return Malformed(error);
        if (LifeSystemHelpers.HouseholdOf(world, economy, genos) is { } existing &&
            existing != Entity.Null &&
            economy.Housing.UnitOf(existing) != Entity.Null)
            return PlayerActionResult.Reject("invalid_state", "You already have a home. Move out first.");

        if (!TryResolveVacantUnit(request!.UnitNetworkId, request.PropertyNetworkId, out var unit))
            return PlayerActionResult.Reject("not_available", "That unit is not available.");

        var data = world.Get<HousingUnit>(unit);
        var property = data.Property;
        if (!Near(genos, property))
            return PlayerActionResult.Reject("out_of_range", "Walk to the property entrance first.");
        if (data.Condition < HousingCostPolicy.UninhabitableThreshold)
            return PlayerActionResult.Reject("not_available", "That unit is uninhabitable.");

        var rent = allocator.MarketRent(unit);
        var wallet = world.Has<Wallet>(genos) ? world.Get<Wallet>(genos).Balance : Money.Zero;
        if (wallet < rent)
            return PlayerActionResult.Reject("insufficient_funds", "You need at least one month's rent.");

        var offer = new PlayerLeaseOffer
        {
            OfferId = Guid.NewGuid(),
            Unit = unit,
            Property = property,
            MonthlyRent = rent,
            CreatedTick = tick,
            ExpiresTick = tick + OfferLifetimeTicks
        };
        if (world.Has<PlayerLeaseOffer>(genos))
            world.Get<PlayerLeaseOffer>(genos) = offer;
        else
            world.Add(genos, offer);

        return PlayerActionResult.Success("Lease offer created.", new
        {
            offer.OfferId,
            UnitNetworkId = NetworkIdOf(unit),
            RentCents = rent.Cents,
            offer.ExpiresTick
        });
    }

    private PlayerActionResult AcceptLease(string playerId, Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.AcceptLease>(payload, out var request, out var error))
            return Malformed(error);
        if (!world.Has<PlayerLeaseOffer>(genos))
            return PlayerActionResult.Reject("offer_not_found", "There is no lease offer to accept.");

        var offer = world.Get<PlayerLeaseOffer>(genos);
        if (request!.OfferId != Guid.Empty && request.OfferId != offer.OfferId)
            return PlayerActionResult.Reject("offer_not_found", "That lease offer is not yours.");
        if (tick > offer.ExpiresTick)
        {
            world.Remove<PlayerLeaseOffer>(genos);
            return PlayerActionResult.Reject("offer_expired", "The lease offer has expired.");
        }

        if (!world.IsAlive(offer.Unit) || !world.Has<HousingUnit>(offer.Unit) ||
            !world.Get<HousingUnit>(offer.Unit).IsVacant)
            return PlayerActionResult.Reject("not_available", "That unit is no longer available.");
        var unit = offer.Unit;

        var household = LifeSystemHelpers.HouseholdOf(world, economy, genos);
        if (household != Entity.Null && economy.Housing.UnitOf(household) != Entity.Null)
            return PlayerActionResult.Reject("invalid_state", "Your household already has a home.");
        if (household != Entity.Null && economy.Housing.MemberCount(household) > 1)
            return PlayerActionResult.Reject("invalid_state", "Leave your shared household before renting.");

        if (household == Entity.Null)
        {
            var householdId = households.Create(NetworkIdOf(genos), tick);
            if (!households.TryGet(householdId, out household))
                return PlayerActionResult.Reject("invalid_state", "A household could not be created.");
        }

        if (!allocator.TryAssign(household, unit, Tenure.Renter, tick, rentOverride: offer.MonthlyRent))
            return PlayerActionResult.Reject("not_available", "The unit could not be assigned.");

        world.Remove<PlayerLeaseOffer>(genos);
        var lease = new PlayerHousing(household, unit, tick, Money.Zero);
        if (world.Has<PlayerHousing>(genos))
            world.Get<PlayerHousing>(genos) = lease;
        else
            world.Add(genos, lease);

        EnsurePantry(household);
        economy.Emit(new PlayerLeaseStarted(
            playerId,
            NetworkIdOf(genos),
            NetworkIdOf(household),
            NetworkIdOf(unit),
            offer.MonthlyRent.Cents,
            tick));

        return PlayerActionResult.Success("You moved in.", new
        {
            UnitNetworkId = NetworkIdOf(unit),
            HouseholdNetworkId = NetworkIdOf(household),
            RentCents = offer.MonthlyRent.Cents
        });
    }

    private PlayerActionResult MoveOut(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.MoveOut>(payload, out var request, out var error))
            return Malformed(error);

        var household = LifeSystemHelpers.HouseholdOf(world, economy, genos);
        if (household == Entity.Null)
            return PlayerActionResult.Reject("no_household", "You do not have a home to leave.");

        var current = economy.Housing.UnitOf(household);
        Entity destination = Entity.Null;
        var sheltered = false;

        if (request!.DestinationUnitNetworkId != 0)
        {
            if (!TryEntity(request.DestinationUnitNetworkId, out destination) ||
                !world.Has<HousingUnit>(destination) ||
                !world.Get<HousingUnit>(destination).IsVacant)
                return PlayerActionResult.Reject("not_available", "That destination unit is not available.");
            if (!Near(genos, world.Get<HousingUnit>(destination).Property))
                return PlayerActionResult.Reject("out_of_range", "Walk to the destination first.");
        }
        else if (!allocator.TryFindBest(household, out destination, out sheltered, excludeUnit: current))
        {
            return PlayerActionResult.Reject("not_available", "There is no available destination.");
        }

        if (destination == Entity.Null || destination == current)
            return PlayerActionResult.Reject("invalid_state", "Choose a different destination.");

        var tenure = sheltered ? Tenure.Sheltered : Tenure.Renter;
        if (!allocator.TryAssign(household, destination, tenure, tick, reference: "housing.move"))
            return PlayerActionResult.Reject("not_available", "The move could not be completed.");

        if (world.Has<PlayerHousing>(genos))
        {
            var lease = world.Get<PlayerHousing>(genos);
            lease.Household = household;
            lease.Unit = destination;
            lease.LeaseStartTick = tick;
            lease.LeaseActive = true;
            world.Get<PlayerHousing>(genos) = lease;
        }

        economy.Emit(new PlayerMoved(
            PlayerIdOf(genos),
            NetworkIdOf(genos),
            NetworkIdOf(household),
            NetworkIdOf(current),
            NetworkIdOf(destination),
            tick));
        return PlayerActionResult.Success("You moved.", new
        {
            UnitNetworkId = NetworkIdOf(destination),
            RentCents = world.Get<Household>(household).MonthlyRent.Cents
        });
    }

    private PlayerActionResult PayArrears(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.PayArrears>(payload, out var request, out var error))
            return Malformed(error);

        var household = LifeSystemHelpers.HouseholdOf(world, economy, genos);
        if (household == Entity.Null)
            return PlayerActionResult.Reject("no_household", "You do not have a home.");

        var data = world.Get<Household>(household);
        var total = data.RentArrears + data.UtilityArrears;
        if (!total.IsPositive)
            return PlayerActionResult.Reject("invalid_state", "There are no arrears to pay.");

        var requested = request!.AmountCents > 0
            ? new Money(request.AmountCents)
            : total;
        if (request.RentOnly)
            requested = requested.Min(data.RentArrears);
        var amount = requested.Min(total);
        if (!amount.IsPositive)
            return PlayerActionResult.Reject("invalid_state", "There are no rent arrears to pay.");

        if (!TryDebitPlayer(genos, amount, LedgerKind.RentArrears, tick, "player.arrears"))
            return PlayerActionResult.Reject("insufficient_funds", "You cannot afford that payment.");

        var rentPaid = amount.Min(data.RentArrears);
        var utilityPaid = (amount - rentPaid).Min(data.UtilityArrears);
        data.RentArrears -= rentPaid;
        data.UtilityArrears -= utilityPaid;
        data.NoUtilities = data.UtilityArrears.IsPositive;
        if (data.RentArrears.IsZero)
        {
            data.ArrearsCycles = 0;
            data.NoticeStage = 0;
            data.NoticeStartedTick = 0;
            data.CureDeadlineTick = 0;
        }
        world.Get<Household>(household) = data;

        CreditLandlord(household, rentPaid, tick);
        economy.Government.Balance += utilityPaid;

        economy.Emit(new PlayerArrearsPaid(NetworkIdOf(genos), NetworkIdOf(household), amount.Cents, tick));
        return PlayerActionResult.Success("Arrears paid.", new { AmountCents = amount.Cents });
    }

    private PlayerActionResult BuyItem(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.BuyItem>(payload, out var request, out var error))
            return Malformed(error);
        if (request!.Quantity <= 0)
            return PlayerActionResult.Reject("invalid_state", "Quantity must be positive.");

        if (!TryResolveStore(request.StoreNetworkId, genos, out var store, out var seller))
            return PlayerActionResult.Reject("not_available", "There is no store in reach.");
        if (!Near(genos, store))
            return PlayerActionResult.Reject("out_of_range", "Walk to the store entrance first.");

        if (!PlayerCatalog.TryGet(request.CatalogItemId, out var item))
            return BuyGroceryItem(genos, request.CatalogItemId, request.Quantity, store, seller, tick);

        if (request.Quantity > item.MaxQuantity)
            return PlayerActionResult.Reject("invalid_state", $"Quantity must be between 1 and {item.MaxQuantity}.");

        var total = item.Price * request.Quantity;
        if (!world.Has<Wallet>(genos) || world.Get<Wallet>(genos).Balance < total)
            return PlayerActionResult.Reject("insufficient_funds", "You cannot afford that purchase.");

        if (!TryDebitPlayer(genos, total, LedgerKind.ConsumerSpending, tick, "player.shop"))
            return PlayerActionResult.Reject("insufficient_funds", "You cannot afford that purchase.");

        CreditSeller(seller, store, total, "Grocery", tick);

        var pantryOwner = PantryOwner(genos);
        var pantry = world.Has<HouseholdPantry>(pantryOwner)
            ? world.Get<HouseholdPantry>(pantryOwner)
            : new HouseholdPantry();
        pantry.FoodUnits += item.FoodUnits * request.Quantity;
        pantry.HouseholdGoods += item.HouseholdGoods * request.Quantity;
        pantry.LastPurchaseTick = tick;
        StorePantry(pantryOwner, pantry);

        var state = world.Has<ConsumptionState>(genos) ? world.Get<ConsumptionState>(genos) : new ConsumptionState();
        if (item.FoodUnits > 0)
            state.FoodPurchased = true;
        if (item.HouseholdGoods > 0)
            state.HygienePurchased = true;
        if (world.Has<ConsumptionState>(genos))
            world.Get<ConsumptionState>(genos) = state;
        else
            world.Add(genos, state);

        economy.Emit(new PlayerBoughtItem(
            NetworkIdOf(genos),
            NetworkIdOf(store),
            item.Id,
            request.Quantity,
            total.Cents,
            tick));

        return PlayerActionResult.Success("Purchase complete.", new
        {
            item.Id,
            request.Quantity,
            AmountCents = total.Cents,
            pantry.FoodUnits,
            pantry.HouseholdGoods
        });
    }

    private PlayerActionResult BuyGroceryItem(
        Entity genos,
        string itemId,
        int quantity,
        Entity store,
        Entity seller,
        long tick)
    {
        if (!GroceryCatalog.TryGet(itemId, out var item))
            return PlayerActionResult.Reject("not_available", "That item is not sold here.");
        if (quantity > 20)
            return PlayerActionResult.Reject("invalid_state", "Quantity must be between 1 and 20.");

        groceryStores.EnsureInventory(store, tick);
        var index = GroceryCatalog.IndexOf(item.Id);
        if (!groceryStores.TrySell(store, index, quantity, out var total))
            return PlayerActionResult.Reject("out_of_stock", "There is not enough stock of that item.");

        if (!TryDebitPlayer(genos, total, LedgerKind.ConsumerSpending, tick, "player.grocery", NetworkIdOf(store)))
            return PlayerActionResult.Reject("insufficient_funds", "You cannot afford that purchase.");

        CreditSeller(seller, store, total, "Grocery", tick);

        var pantryOwner = PantryOwner(genos);
        var pantry = world.Has<HouseholdPantry>(pantryOwner)
            ? world.Get<HouseholdPantry>(pantryOwner)
            : new HouseholdPantry();
        pantry.FoodUnits += item.PantryUnits * quantity;
        pantry.LastPurchaseTick = tick;
        StorePantry(pantryOwner, pantry);

        var state = world.Has<ConsumptionState>(genos) ? world.Get<ConsumptionState>(genos) : new ConsumptionState();
        state.FoodPurchased = true;
        if (world.Has<ConsumptionState>(genos))
            world.Get<ConsumptionState>(genos) = state;
        else
            world.Add(genos, state);

        var nutrition = world.Has<NutritionState>(genos) ? world.Get<NutritionState>(genos) : new NutritionState();
        nutrition.Balance = Math.Clamp(nutrition.Balance + item.Nutrition * quantity * 2, -100, 100);
        nutrition.LastMealTick = tick;
        if (world.Has<NutritionState>(genos))
            world.Get<NutritionState>(genos) = nutrition;
        else
            world.Add(genos, nutrition);

        economy.Emit(new PlayerBoughtItem(
            NetworkIdOf(genos),
            NetworkIdOf(store),
            item.Id,
            quantity,
            total.Cents,
            tick));
        economy.Emit(new GroceryPurchase(
            NetworkIdOf(PantryOwner(genos)),
            NetworkIdOf(store),
            item.Id,
            quantity,
            total.Cents,
            tick));

        return PlayerActionResult.Success("Purchase complete.", new
        {
            item.Id,
            quantity,
            AmountCents = total.Cents,
            pantry.FoodUnits
        });
    }

    private PlayerActionResult ConsumeItem(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.ConsumeItem>(payload, out var request, out var error))
            return Malformed(error);
        if (!PlayerCatalog.TryGet(request!.CatalogItemId, out var item))
            return ConsumeGroceryItem(genos, request.CatalogItemId, request.Quantity, tick);
        if (request.Quantity <= 0)
            return PlayerActionResult.Reject("invalid_state", "Quantity must be positive.");

        var pantryOwner = PantryOwner(genos);
        if (!world.Has<HouseholdPantry>(pantryOwner))
            return PlayerActionResult.Reject("pantry_empty", "The pantry is empty.");

        var pantry = world.Get<HouseholdPantry>(pantryOwner);
        if (item.FoodUnits > 0 && pantry.FoodUnits < request.Quantity)
            return PlayerActionResult.Reject("pantry_empty", "There is not enough food in the pantry.");
        if (item.HouseholdGoods > 0 && pantry.HouseholdGoods < request.Quantity)
            return PlayerActionResult.Reject("pantry_empty", "There are not enough household goods in the pantry.");

        pantry.FoodUnits = Math.Max(0, pantry.FoodUnits - item.FoodUnits * request.Quantity);
        pantry.HouseholdGoods = Math.Max(0, pantry.HouseholdGoods - item.HouseholdGoods * request.Quantity);
        StorePantry(pantryOwner, pantry);

        if (world.Has<Needs>(genos))
        {
            var needs = world.Get<Needs>(genos);
            needs.Hunger = Math.Min(100f, needs.Hunger + item.Hunger * request.Quantity);
            needs.Hygiene = Math.Min(100f, needs.Hygiene + item.Hygiene * request.Quantity);
            world.Get<Needs>(genos) = needs;
        }

        economy.Emit(new PlayerConsumedItem(NetworkIdOf(genos), item.Id, request.Quantity, tick));
        return PlayerActionResult.Success("Item consumed.", new { item.Id, request.Quantity });
    }

    private PlayerActionResult ConsumeGroceryItem(Entity genos, string itemId, int quantity, long tick)
    {
        if (!GroceryCatalog.TryGet(itemId, out var item))
            return PlayerActionResult.Reject("not_available", "That item is not in the pantry.");
        if (quantity <= 0)
            return PlayerActionResult.Reject("invalid_state", "Quantity must be positive.");

        var pantryOwner = PantryOwner(genos);
        if (!world.Has<HouseholdPantry>(pantryOwner))
            return PlayerActionResult.Reject("pantry_empty", "The pantry is empty.");

        var pantry = world.Get<HouseholdPantry>(pantryOwner);
        var required = item.PantryUnits * quantity;
        if (item.Category != GroceryCategory.Household && pantry.FoodUnits < required)
            return PlayerActionResult.Reject("pantry_empty", "There is not enough food in the pantry.");

        if (item.Category != GroceryCategory.Household)
            pantry.FoodUnits = Math.Max(0, pantry.FoodUnits - required);
        StorePantry(pantryOwner, pantry);

        if (world.Has<Needs>(genos))
        {
            var needs = world.Get<Needs>(genos);
            needs.Hunger = Math.Min(100f, needs.Hunger + item.Nutrition * 8f * quantity);
            if (item.Category == GroceryCategory.Household)
                needs.Hygiene = Math.Min(100f, needs.Hygiene + 25f * quantity);
            world.Get<Needs>(genos) = needs;
        }

        var nutrition = world.Has<NutritionState>(genos) ? world.Get<NutritionState>(genos) : new NutritionState();
        var balanced = item.Category is GroceryCategory.Staple or GroceryCategory.Produce or GroceryCategory.Protein;
        nutrition.Balance = Math.Clamp(nutrition.Balance + (balanced ? 8 : 2) * quantity, -100, 100);
        nutrition.LastMealTick = tick;
        if (nutrition.Balance < 0) nutrition.ConsecutivePoorDays++;
        else nutrition.ConsecutivePoorDays = 0;
        if (world.Has<NutritionState>(genos))
            world.Get<NutritionState>(genos) = nutrition;
        else
            world.Add(genos, nutrition);

        economy.Emit(new PlayerConsumedItem(NetworkIdOf(genos), item.Id, quantity, tick));
        return PlayerActionResult.Success("Item consumed.", new { item.Id, quantity });
    }

    private PlayerActionResult InvitePartner(string playerId, Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.InvitePartner>(payload, out var request, out var error))
            return Malformed(error);

        Entity target;
        if (!string.IsNullOrWhiteSpace(request!.TargetPlayerId))
        {
            if (!players.TryGet(request.TargetPlayerId, out var requestedProfile) ||
                requestedProfile.ControlledGenos == Entity.Null ||
                !world.IsAlive(requestedProfile.ControlledGenos))
                return PlayerActionResult.Reject("target_not_found", "That player is not available.");
            target = requestedProfile.ControlledGenos;
        }
        else if (!TryEntity(request.TargetGenosNetworkId, out target))
        {
            return PlayerActionResult.Reject("target_not_found", "That genos is not available.");
        }
        if (target == genos)
            return PlayerActionResult.Reject("invalid_state", "You cannot invite yourself.");
        if (world.Has<Dead>(target))
            return PlayerActionResult.Reject("not_available", "That genos is not available.");

        if (players.TryGetByGenos(target, out var targetProfile) &&
            !string.Equals(targetProfile.PlayerId, playerId, StringComparison.Ordinal))
        {
            var playerInvite = playerRelationships.InvitePartner(playerId, targetProfile.PlayerId, tick);
            return MapRelationshipResult(playerInvite);
        }

        var eligibility = EligibilityPolicy.Evaluate(
            world,
            relationships,
            genos,
            target,
            relationshipOptions,
            (first, second) => InConsentCooldown(second, first, tick));
        if (!eligibility.Eligible)
            return PlayerActionResult.Reject(eligibility.Code ?? "invalid_state", eligibility.Reason ?? "Not eligible.");

        if (HasPendingInvitation(genos, target))
            return PlayerActionResult.Reject("invalid_state", "An invitation is already pending.");

        var invitation = CreateInvitation(genos, target, FamilyInvitationKind.Partnership, tick);
        var invitationId = world.Get<FamilyInvitation>(invitation).Id;
        economy.Emit(new PlayerInvited(
            NetworkIdOf(genos),
            NetworkIdOf(target),
            invitationId,
            FamilyInvitationKind.Partnership.ToString(),
            world.Get<FamilyInvitation>(invitation).ExpiresTick,
            tick));

        var consent = EvaluateNpcConsent(genos, target, tick);
        var data = world.Get<FamilyInvitation>(invitation);
        if (consent.Accepted)
        {
            data.Status = FamilyInvitationStatus.Accepted;
            world.Get<FamilyInvitation>(invitation) = data;
            AcceptPartnership(genos, target, tick, consent.Score);
            economy.Emit(new PlayerInvitationResponded(
                NetworkIdOf(genos), NetworkIdOf(target), invitationId,
                FamilyInvitationKind.Partnership.ToString(), true, null, tick));
            return PlayerActionResult.Success("Your invitation was accepted.", new { invitationId, accepted = true });
        }

        data.Status = FamilyInvitationStatus.Declined;
        world.Get<FamilyInvitation>(invitation) = data;
        SetConsentCooldown(target, genos, tick);
        economy.Emit(new PlayerInvitationResponded(
            NetworkIdOf(genos), NetworkIdOf(target), invitationId,
            FamilyInvitationKind.Partnership.ToString(), false, consent.Code, tick));
        return PlayerActionResult.Reject("declined", consent.Message);
    }

    private PlayerActionResult InviteCohabit(string playerId, Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.InviteCohabit>(payload, out var request, out var error))
            return Malformed(error);

        var targetPlayerId = request!.TargetPlayerId?.Trim();
        if (string.IsNullOrWhiteSpace(targetPlayerId))
            return PlayerActionResult.Reject("target_required", "Choose a partner to cohabit with.");
        if (string.Equals(targetPlayerId, playerId, StringComparison.Ordinal))
            return PlayerActionResult.Reject("self_target", "You cannot invite yourself.");

        return MapRelationshipResult(playerRelationships.InviteCohabit(playerId, targetPlayerId, tick));
    }

    private PlayerActionResult EndPartnership(string playerId, Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.EndPartnership>(payload, out var request, out var error))
            return Malformed(error);

        var partnerPlayerId = request!.PartnerPlayerId?.Trim();
        if (string.IsNullOrWhiteSpace(partnerPlayerId))
            return PlayerActionResult.Reject("target_required", "Choose the partner to end the partnership with.");

        return MapRelationshipResult(playerRelationships.EndPartnership(
            playerId,
            partnerPlayerId,
            tick,
            request.SplitHousehold));
    }

    private PlayerActionResult PerformVenueActivity(string playerId, Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.PerformVenueActivity>(payload, out var request, out var error))
            return Malformed(error);

        if (!Enum.TryParse<Genelife.Components.Social.VenueActivity>(
                request!.Activity, ignoreCase: true, out var activity))
            return PlayerActionResult.Reject("invalid_activity", "Unknown venue activity.");

        var venue = venues.VenueOf(playerId);
        if (venue == Entity.Null)
            return PlayerActionResult.Reject("not_in_venue", "Enter a social venue first.");

        var result = venueActivities.TryPerform(playerId, venue, activity, tick);
        if (!result.Applied)
            return PlayerActionResult.Reject(result.Code ?? "invalid_state", result.Message ?? "Activity not allowed.");

        return PlayerActionResult.Success(
            "Activity performed.",
            new
            {
                venueNetworkId = NetworkIdOf(venue),
                activity = activity.ToString(),
                recipients = result.Recipients
            });
    }

    private static PlayerActionResult MapRelationshipResult(PlayerRelationshipResult result)
    {
        if (!result.Applied)
            return PlayerActionResult.Reject(result.Code ?? "invalid_state", result.Message ?? "Not available.");

        object? data = result.Data;
        if (result.Invitation is not null)
            data ??= new { invitationId = result.Invitation.Id, pendingConsent = true };
        return PlayerActionResult.Success(result.Message, data);
    }

    private PlayerActionResult RespondToInvitation(string playerId, Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.RespondToInvitation>(payload, out var request, out var error))
            return Malformed(error);
        if (!TryFindInvitation(request!.InvitationId, out var entity))
        {
            var playerInvitation = playerRelationships.Respond(
                playerId,
                request.InvitationId,
                request.Accept,
                tick);
            if (playerInvitation.Applied || playerInvitation.Code != "offer_not_found")
                return MapRelationshipResult(playerInvitation);
            return PlayerActionResult.Reject("offer_not_found", "That invitation no longer exists.");
        }

        var invitation = world.Get<FamilyInvitation>(entity);
        if (invitation.To != genos)
            return PlayerActionResult.Reject("not_target", "That invitation was not sent to you.");
        if (invitation.Status != FamilyInvitationStatus.Pending)
            return PlayerActionResult.Reject("invalid_state", "That invitation is no longer pending.");
        if (tick > invitation.ExpiresTick)
        {
            invitation.Status = FamilyInvitationStatus.Expired;
            world.Get<FamilyInvitation>(entity) = invitation;
            economy.Emit(new PlayerInvitationResponded(
                NetworkIdOf(invitation.From), NetworkIdOf(invitation.To), invitation.Id,
                invitation.Kind.ToString(), false, "expired", tick));
            return PlayerActionResult.Reject("offer_expired", "That invitation has expired.");
        }

        if (!request.Accept)
        {
            invitation.Status = FamilyInvitationStatus.Declined;
            world.Get<FamilyInvitation>(entity) = invitation;
            SetConsentCooldown(invitation.From, invitation.To, tick);
            economy.Emit(new PlayerInvitationResponded(
                NetworkIdOf(invitation.From), NetworkIdOf(invitation.To), invitation.Id,
                invitation.Kind.ToString(), false, "declined", tick));
            return PlayerActionResult.Success("Invitation declined.");
        }

        switch (invitation.Kind)
        {
            case FamilyInvitationKind.Partnership:
                if (!AcceptPartnership(invitation.From, invitation.To, tick, 0))
                    return PlayerActionResult.Reject("invalid_state", "The partnership could not be formed.");
                break;
            case FamilyInvitationKind.Conception:
            {
                var conception = TryConceive(invitation.From, invitation.To, tick);
                if (!conception.Applied)
                    return conception;
                break;
            }
            default:
                return PlayerActionResult.Reject("not_available", "That invitation kind is not supported yet.");
        }

        invitation.Status = FamilyInvitationStatus.Accepted;
        world.Get<FamilyInvitation>(entity) = invitation;
        economy.Emit(new PlayerInvitationResponded(
            NetworkIdOf(invitation.From), NetworkIdOf(invitation.To), invitation.Id,
            invitation.Kind.ToString(), true, null, tick));
        return PlayerActionResult.Success("Invitation accepted.");
    }

    private PlayerActionResult TryForChild(string playerId, Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.TryForChild>(payload, out var request, out var error))
            return Malformed(error);
        if (!TryEntity(request!.PartnerGenosNetworkId, out var partner))
            return PlayerActionResult.Reject("target_not_found", "That genos is not available.");
        if (!relationships.PartnersOf(genos).Contains(partner) || !relationships.PartnersOf(partner).Contains(genos))
            return PlayerActionResult.Reject("not_partner", "You need an active partnership first.");

        var partnerIsOtherPlayer = players.TryGetByGenos(partner, out var partnerProfile) &&
                                   !string.Equals(partnerProfile.PlayerId, playerId, StringComparison.Ordinal);
        if (partnerIsOtherPlayer)
        {
            if (HasPendingInvitation(genos, partner, FamilyInvitationKind.Conception))
                return PlayerActionResult.Reject("invalid_state", "A conception invitation is already pending.");

            var invitation = CreateInvitation(genos, partner, FamilyInvitationKind.Conception, tick);
            var id = world.Get<FamilyInvitation>(invitation).Id;
            economy.Emit(new PlayerInvited(
                NetworkIdOf(genos),
                NetworkIdOf(partner),
                id,
                FamilyInvitationKind.Conception.ToString(),
                world.Get<FamilyInvitation>(invitation).ExpiresTick,
                tick));
            return PlayerActionResult.Success("Consent requested.", new { invitationId = id, pendingConsent = true });
        }

        return TryConceive(genos, partner, tick);
    }

    private PlayerActionResult NameChild(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.NameChild>(payload, out var request, out var error))
            return Malformed(error);
        if (!TryEntity(request!.ChildNetworkId, out var child) || !world.Has<GenName>(child))
            return PlayerActionResult.Reject("target_not_found", "That child is not available.");
        if (world.Has<Dead>(child))
            return PlayerActionResult.Reject("not_available", "That child is not available.");

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 40)
            return PlayerActionResult.Reject("invalid_state", "A name between 1 and 40 characters is required.");

        var isParent = relationships.IsParentOf(genos, child);
        var household = LifeSystemHelpers.HouseholdOf(world, economy, child);
        var sameHousehold = household != Entity.Null &&
                            LifeSystemHelpers.HouseholdOf(world, economy, genos) == household;
        if (!isParent && !sameHousehold)
            return PlayerActionResult.Reject("invalid_state", "You can only name your own child.");

        world.Get<GenName>(child) = new GenName(name);
        economy.Emit(new PlayerChildNamed(NetworkIdOf(genos), NetworkIdOf(child), name, tick));
        return PlayerActionResult.Success("Child named.", new { name });
    }

    private PlayerActionResult ChooseContinuation(string playerId, Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.ChooseContinuation>(payload, out var request, out var error))
            return Malformed(error);
        if (!Enum.TryParse<PlayerContinuationPolicy>(request!.Policy, ignoreCase: true, out var policy))
            return PlayerActionResult.Reject("invalid_state", "Unknown continuation policy.");

        var profile = players.GetOrCreate(playerId, genos, tick);
        profile.ContinuationPolicy = policy;
        profile.LastActionTick = tick;
        return PlayerActionResult.Success("Continuation preference saved.", new { policy = policy.ToString() });
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

    private PlayerActionResult TryConceive(Entity first, Entity second, long tick)
    {
        var carrying = ChooseCarryingParent(first, second);
        if (carrying == Entity.Null)
            return PlayerActionResult.Reject("invalid_state", "This pair cannot conceive under the current policy.");
        var other = carrying == first ? second : first;

        if (!CanConceive(carrying, other, tick, out var reason))
            return PlayerActionResult.Reject("invalid_state", reason);

        var firstId = NetworkIdOf(first);
        var secondId = NetworkIdOf(second);
        var key = firstId <= secondId ? (firstId, secondId) : (secondId, firstId);
        var stream = $"reproduction.conception.{key.Item1}.{key.Item2}." +
                     $"{economy.Clock.YearOf(tick)}.{economy.Clock.MonthOfYear(tick)}";
        if (economy.Random.For(stream).NextDouble() >= (double)reproductionOptions.MonthlyConceptionChance)
            return PlayerActionResult.Reject("not_conceived", "No pregnancy occurred this month.");

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
            return PlayerActionResult.Reject("invalid_state", pregnancyError.ToString());

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
        PlayerActionResult.Reject("malformed_payload", error ?? "The payload could not be read.");

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
