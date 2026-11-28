using System.Globalization;
using System.Text.Json;
using Arch.Core;
using Genelife.Api.Commands;
using Genelife.Components.Company;
using Genelife.Components.Consumption;
using Genelife.Components.Employment;
using Genelife.Components.Family;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Components.Map;
using Genelife.Components.Player;
using Genelife.Components.Social;
using Genelife.Consumption;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Housing;
using Genelife.Map;
using Genelife.Messages.Events;
using Genelife.Player;
using Genelife.Social;
using Genelife.Values;
using Genelife.Application.Systems;

namespace Genelife.Api.Services;

/// <summary>
/// The action rule source: <see cref="Evaluate"/> is the ONE precondition evaluation, called by the
/// catalogue read model and by <see cref="PlayerActionService.Apply"/> alike (GDD chapter 08
/// §8.5(1); chapter 07 §7.6 AC-4a). Everything after it mutates the world with the facts the
/// evaluation resolved, so a verb the catalogue offers cannot be refused by a second, parallel list.
/// </summary>
public sealed partial class PlayerActionService
{
    /// <summary>
    /// Pure precondition evaluation for one kind. Reads the world, mutates nothing and consumes no
    /// randomness: stochastic outcomes (conception roll, NPC consent) belong to the apply phase, not
    /// to availability. Costs and durations come from domain options and the pathfinder.
    /// </summary>
    public ActionEvaluation Evaluate(Entity genos, PlayerActionKind kind, JsonElement payload, long tick)
    {
        if (world is null || !world.IsAlive(genos))
            return ActionEvaluation.Refuse(PlayerRefusal.GenosNotFound, "Your genos is not available.");
        if (world.Has<Dead>(genos))
            return ActionEvaluation.Refuse(PlayerRefusal.GenosNotFound, "Your genos is dead.");

        return kind switch
        {
            PlayerActionKind.ApplyForJob => EvaluateApplyForJob(genos, payload, tick),
            PlayerActionKind.AcceptJobOffer => EvaluateAcceptJobOffer(genos, payload, tick),
            PlayerActionKind.QuitJob => EvaluateQuitJob(genos, payload),
            PlayerActionKind.RequestHousing => EvaluateRequestHousing(genos, payload, tick),
            PlayerActionKind.AcceptLease => EvaluateAcceptLease(genos, payload, tick),
            PlayerActionKind.MoveOut => EvaluateMoveOut(genos, payload),
            PlayerActionKind.PayArrears => EvaluatePayArrears(genos, payload),
            PlayerActionKind.BuyItem => EvaluateBuyItem(genos, payload),
            PlayerActionKind.ConsumeItem => EvaluateConsumeItem(genos, payload),
            PlayerActionKind.InvitePartner => EvaluateInvitePartner(genos, payload, tick),
            PlayerActionKind.InviteCohabit => EvaluateInviteCohabit(genos, payload, tick),
            PlayerActionKind.EndPartnership => EvaluateEndPartnership(genos, payload),
            PlayerActionKind.PerformVenueActivity => EvaluatePerformVenueActivity(genos, payload, tick),
            PlayerActionKind.RespondToInvitation => EvaluateRespondToInvitation(genos, payload, tick),
            PlayerActionKind.TryForChild => EvaluateTryForChild(genos, payload, tick),
            PlayerActionKind.NameChild => EvaluateNameChild(genos, payload),
            PlayerActionKind.ChooseContinuation => EvaluateChooseContinuation(payload),
            _ => ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "Unknown player action.")
        };
    }

    /// <summary>
    /// The catalogue read model for one player in one context. Every entry is the
    /// <see cref="Evaluate"/> result for that kind — same allowed flag, same refusalCode, same cost
    /// and duration — so the dial can never disagree with the tick. Costs/durations are domain
    /// facts; the client derives nothing.
    /// </summary>
    public ActionCatalogue BuildCatalogue(string playerId, ActionCatalogueRequest request, long tick)
    {
        var asOf = request.AsOfTick ?? tick;
        var genos = Entity.Null;
        ActionEvaluation ownerRefusal = default;
        var owned = false;

        if (world is null || !players.TryGet(playerId, out var profile) || profile.ControlledGenos == Entity.Null)
        {
            ownerRefusal = ActionEvaluation.Refuse(PlayerRefusal.WorldNotReady, "The world is not ready yet.");
        }
        else if (!world.IsAlive(profile.ControlledGenos))
        {
            ownerRefusal = ActionEvaluation.Refuse(PlayerRefusal.GenosNotFound, "Your genos is not available.");
        }
        else if (request.GenosNetworkId != 0 && NetworkIdOf(profile.ControlledGenos) != request.GenosNetworkId)
        {
            ownerRefusal = ActionEvaluation.Refuse(PlayerRefusal.NotOwner, "You do not control that genos.");
        }
        else
        {
            genos = profile.ControlledGenos;
            owned = true;
        }

        var targetSource = request.Targets ?? (owned ? DefaultCatalogueTargets(genos, asOf) : null);
        var targets = new Dictionary<PlayerActionKind, JsonElement>();
        if (targetSource is not null)
        {
            foreach (var target in targetSource)
                targets[target.Kind] = target.Payload;
        }

        var entries = new List<ActionCatalogueEntry>();
        foreach (var kind in Enum.GetValues<PlayerActionKind>().OrderBy(value => (byte)value))
        {
            var evaluation = owned
                ? Evaluate(genos, kind, targets.TryGetValue(kind, out var payload) ? payload : ActionCataloguePayloads.Empty(kind), asOf)
                : ownerRefusal;

            entries.Add(new ActionCatalogueEntry(
                kind,
                evaluation.Allowed,
                evaluation.RefusalCode,
                evaluation.CostCents,
                evaluation.DurationTicks,
                evaluation.TargetHint,
                asOf));
        }

        return new ActionCatalogue(
            playerId,
            request.GenosNetworkId != 0 ? request.GenosNetworkId : NetworkIdOf(genos),
            ActionCatalogueVocabulary.ShapeVersion,
            ActionCatalogueVocabulary.RefusalEnumVersion,
            asOf,
            entries);
    }

    // ---------------------------------------------------------------- evaluations

    private ActionEvaluation EvaluateApplyForJob(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.ApplyForJob>(payload, out var request, out var error))
            return RefuseMalformed(error);

        if (!TryEntity(request!.OfficeNetworkId, out var office) || !world.Has<Office>(office))
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "That office is not available.");
        if (!Near(genos, office))
            return ActionEvaluation.Refuse(PlayerRefusal.OutOfRange, "Walk to the office entrance first.");
        if (world.Has<Employee>(genos))
            return ActionEvaluation.Refuse(PlayerRefusal.AlreadyEmployed, "You already have a job.");

        Entity postingEntity;
        if (request.PostingNetworkId != 0)
        {
            if (!TryEntity(request.PostingNetworkId, out postingEntity) ||
                !world.Has<JobPosting>(postingEntity))
                return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "That posting no longer exists.");
        }
        else if (!TryFirstOpenPosting(office, out postingEntity))
        {
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "There are no open postings at this office.");
        }

        var posting = world.Get<JobPosting>(postingEntity);
        if (posting.Office != office || posting.IsClosed || posting.OpenSeats <= 0)
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "That posting is not open.");
        if (world.Has<Lifecycle>(genos) && world.Get<Lifecycle>(genos).Stage != LifeStage.Work)
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "Only working-age genoses can apply.");
        if (!MeetsRequirements(genos, posting))
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "You do not meet the posting requirements.");
        if (!CompanyHiringAllowed(posting.Company, out var hiringError))
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, hiringError);

        return ActionEvaluation.Allow(
            durationTicks: OfferLifetimeTicks,
            targetHint: Hint("office", NetworkIdOf(office)),
            plan: new JobApplicationPlan(office, postingEntity, posting, tick + OfferLifetimeTicks));
    }

    private ActionEvaluation EvaluateAcceptJobOffer(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.AcceptJobOffer>(payload, out var request, out var error))
            return RefuseMalformed(error);
        if (!world.Has<PlayerJobOffer>(genos))
            return ActionEvaluation.Refuse(PlayerRefusal.OfferNotFound, "There is no job offer to accept.");

        var offer = world.Get<PlayerJobOffer>(genos);
        if (request!.OfferId != Guid.Empty && request.OfferId != offer.OfferId)
            return ActionEvaluation.Refuse(PlayerRefusal.OfferNotFound, "That job offer is not yours.");
        if (tick > offer.ExpiresTick)
            return ActionEvaluation.Refuse(PlayerRefusal.OfferExpired, "The job offer has expired.");

        if (!world.IsAlive(offer.Posting) || !world.Has<JobPosting>(offer.Posting))
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "The posting is no longer available.");
        var posting = world.Get<JobPosting>(offer.Posting);
        if (posting.IsClosed || posting.OpenSeats <= 0 || !HasOpenSeat(offer.Posting))
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "The posting is no longer available.");
        if (!CompanyHiringAllowed(posting.Company, out var hiringError))
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, hiringError);

        return ActionEvaluation.Allow(
            durationTicks: Math.Max(0, offer.ExpiresTick - tick),
            targetHint: Hint("posting", NetworkIdOf(offer.Posting)),
            plan: new JobOfferPlan(offer));
    }

    private ActionEvaluation EvaluateQuitJob(Entity genos, JsonElement payload)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.QuitJob>(payload, out var request, out var error))
            return RefuseMalformed(error);
        if (!world.Has<Employee>(genos))
            return ActionEvaluation.Refuse(PlayerRefusal.NotEmployed, "You do not have a job.");

        var reason = string.IsNullOrWhiteSpace(request!.Reason) ? "player.quit" : request.Reason!.Trim();
        return ActionEvaluation.Allow(plan: new QuitJobPlan(reason));
    }

    private ActionEvaluation EvaluateRequestHousing(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.RequestHousing>(payload, out var request, out var error))
            return RefuseMalformed(error);
        if (LifeSystemHelpers.HouseholdOf(world, economy, genos) is { } existing &&
            existing != Entity.Null &&
            economy.Housing.UnitOf(existing) != Entity.Null)
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "You already have a home. Move out first.");

        if (!TryResolveVacantUnit(request!.UnitNetworkId, request.PropertyNetworkId, out var unit))
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "That unit is not available.");

        var data = world.Get<HousingUnit>(unit);
        var property = data.Property;
        if (!Near(genos, property))
            return ActionEvaluation.Refuse(PlayerRefusal.OutOfRange, "Walk to the property entrance first.");
        if (data.Condition < HousingCostPolicy.UninhabitableThreshold)
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "That unit is uninhabitable.");

        var rent = allocator.MarketRent(unit);
        var wallet = world.Has<Wallet>(genos) ? world.Get<Wallet>(genos).Balance : Money.Zero;
        if (wallet < rent)
            return ActionEvaluation.Refuse(PlayerRefusal.InsufficientFunds, "You need at least one month's rent.");

        return ActionEvaluation.Allow(
            costCents: rent.Cents,
            durationTicks: OfferLifetimeTicks,
            targetHint: Hint("unit", NetworkIdOf(unit)),
            plan: new HousingOfferPlan(unit, property, rent, tick + OfferLifetimeTicks));
    }

    private ActionEvaluation EvaluateAcceptLease(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.AcceptLease>(payload, out var request, out var error))
            return RefuseMalformed(error);
        if (!world.Has<PlayerLeaseOffer>(genos))
            return ActionEvaluation.Refuse(PlayerRefusal.OfferNotFound, "There is no lease offer to accept.");

        var offer = world.Get<PlayerLeaseOffer>(genos);
        if (request!.OfferId != Guid.Empty && request.OfferId != offer.OfferId)
            return ActionEvaluation.Refuse(PlayerRefusal.OfferNotFound, "That lease offer is not yours.");
        if (tick > offer.ExpiresTick)
            return ActionEvaluation.Refuse(PlayerRefusal.OfferExpired, "The lease offer has expired.");

        if (!world.IsAlive(offer.Unit) || !world.Has<HousingUnit>(offer.Unit) ||
            !world.Get<HousingUnit>(offer.Unit).IsVacant)
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "That unit is no longer available.");

        var household = LifeSystemHelpers.HouseholdOf(world, economy, genos);
        if (household != Entity.Null && economy.Housing.UnitOf(household) != Entity.Null)
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "Your household already has a home.");
        if (household != Entity.Null && economy.Housing.MemberCount(household) > 1)
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "Leave your shared household before renting.");

        return ActionEvaluation.Allow(
            costCents: offer.MonthlyRent.Cents,
            durationTicks: Math.Max(0, offer.ExpiresTick - tick),
            targetHint: Hint("unit", NetworkIdOf(offer.Unit)),
            plan: new LeaseOfferPlan(offer));
    }

    private ActionEvaluation EvaluateMoveOut(Entity genos, JsonElement payload)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.MoveOut>(payload, out var request, out var error))
            return RefuseMalformed(error);

        var household = LifeSystemHelpers.HouseholdOf(world, economy, genos);
        if (household == Entity.Null)
            return ActionEvaluation.Refuse(PlayerRefusal.NoHousehold, "You do not have a home to leave.");

        var current = economy.Housing.UnitOf(household);
        var destination = Entity.Null;
        var sheltered = false;

        if (request!.DestinationUnitNetworkId != 0)
        {
            if (!TryEntity(request.DestinationUnitNetworkId, out destination) ||
                !world.Has<HousingUnit>(destination) ||
                !world.Get<HousingUnit>(destination).IsVacant)
                return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "That destination unit is not available.");
            if (!Near(genos, world.Get<HousingUnit>(destination).Property))
                return ActionEvaluation.Refuse(PlayerRefusal.OutOfRange, "Walk to the destination first.");
        }
        else if (!allocator.TryFindBest(household, out destination, out sheltered, excludeUnit: current))
        {
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "There is no available destination.");
        }

        if (destination == Entity.Null || destination == current)
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "Choose a different destination.");

        var travel = request.DestinationUnitNetworkId != 0
            ? TravelTicks(genos, world.Get<HousingUnit>(destination).Property)
            : 0;
        return ActionEvaluation.Allow(
            durationTicks: travel,
            targetHint: Hint("unit", NetworkIdOf(destination)),
            plan: new MoveOutPlan(household, destination, sheltered));
    }

    private ActionEvaluation EvaluatePayArrears(Entity genos, JsonElement payload)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.PayArrears>(payload, out var request, out var error))
            return RefuseMalformed(error);

        var household = LifeSystemHelpers.HouseholdOf(world, economy, genos);
        if (household == Entity.Null)
            return ActionEvaluation.Refuse(PlayerRefusal.NoHousehold, "You do not have a home.");

        var data = world.Get<Household>(household);
        var total = data.RentArrears + data.UtilityArrears;
        if (!total.IsPositive)
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "There are no arrears to pay.");

        var requested = request!.AmountCents > 0
            ? new Money(request.AmountCents)
            : total;
        if (request.RentOnly)
            requested = requested.Min(data.RentArrears);
        var amount = requested.Min(total);
        if (!amount.IsPositive)
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "There are no rent arrears to pay.");
        if (!CanAfford(genos, amount))
            return ActionEvaluation.Refuse(PlayerRefusal.InsufficientFunds, "You cannot afford that payment.");

        var rentPaid = amount.Min(data.RentArrears);
        var utilityPaid = (amount - rentPaid).Min(data.UtilityArrears);
        return ActionEvaluation.Allow(
            costCents: amount.Cents,
            targetHint: Hint("household", NetworkIdOf(household)),
            plan: new ArrearsPlan(household, amount, rentPaid, utilityPaid));
    }

    private ActionEvaluation EvaluateBuyItem(Entity genos, JsonElement payload)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.BuyItem>(payload, out var request, out var error))
            return RefuseMalformed(error);
        if (request!.Quantity <= 0)
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "Quantity must be positive.");

        if (!TryResolveStore(request.StoreNetworkId, genos, out var store, out var seller))
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "There is no store in reach.");
        if (!Near(genos, store))
            return ActionEvaluation.Refuse(PlayerRefusal.OutOfRange, "Walk to the store entrance first.");

        if (PlayerCatalog.TryGet(request.CatalogItemId, out var item))
        {
            if (request.Quantity > item.MaxQuantity)
                return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, $"Quantity must be between 1 and {item.MaxQuantity}.");

            var total = item.Price * request.Quantity;
            if (!world.Has<Wallet>(genos) || world.Get<Wallet>(genos).Balance < total)
                return ActionEvaluation.Refuse(PlayerRefusal.InsufficientFunds, "You cannot afford that purchase.");

            return ActionEvaluation.Allow(
                costCents: total.Cents,
                durationTicks: TravelTicks(genos, store),
                targetHint: Hint("store", NetworkIdOf(store)),
                plan: new BuyItemPlan(store, seller, item.Id, request.Quantity, total, Grocery: false));
        }

        if (!GroceryCatalog.TryGet(request.CatalogItemId, out var grocery))
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "That item is not sold here.");
        if (request.Quantity > 20)
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "Quantity must be between 1 and 20.");

        var index = GroceryCatalog.IndexOf(grocery.Id);
        if (!groceryStores.TryQuote(store, index, request.Quantity, out var groceryTotal))
            return ActionEvaluation.Refuse(PlayerRefusal.OutOfStock, "There is not enough stock of that item.");
        if (!CanAfford(genos, groceryTotal))
            return ActionEvaluation.Refuse(PlayerRefusal.InsufficientFunds, "You cannot afford that purchase.");

        return ActionEvaluation.Allow(
            costCents: groceryTotal.Cents,
            durationTicks: TravelTicks(genos, store),
            targetHint: Hint("store", NetworkIdOf(store)),
            plan: new BuyItemPlan(store, seller, grocery.Id, request.Quantity, groceryTotal, Grocery: true));
    }

    private ActionEvaluation EvaluateConsumeItem(Entity genos, JsonElement payload)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.ConsumeItem>(payload, out var request, out var error))
            return RefuseMalformed(error);

        if (PlayerCatalog.TryGet(request!.CatalogItemId, out var item))
        {
            if (request.Quantity <= 0)
                return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "Quantity must be positive.");

            var pantryOwner = PantryOwner(genos);
            if (!world.Has<HouseholdPantry>(pantryOwner))
                return ActionEvaluation.Refuse(PlayerRefusal.PantryEmpty, "The pantry is empty.");

            var pantry = world.Get<HouseholdPantry>(pantryOwner);
            if (item.FoodUnits > 0 && pantry.FoodUnits < request.Quantity)
                return ActionEvaluation.Refuse(PlayerRefusal.PantryEmpty, "There is not enough food in the pantry.");
            if (item.HouseholdGoods > 0 && pantry.HouseholdGoods < request.Quantity)
                return ActionEvaluation.Refuse(PlayerRefusal.PantryEmpty, "There are not enough household goods in the pantry.");

            return ActionEvaluation.Allow(plan: new ConsumeItemPlan(item.Id, request.Quantity, Grocery: false));
        }

        if (!GroceryCatalog.TryGet(request.CatalogItemId, out var grocery))
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "That item is not in the pantry.");
        if (request.Quantity <= 0)
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "Quantity must be positive.");

        var owner = PantryOwner(genos);
        if (!world.Has<HouseholdPantry>(owner))
            return ActionEvaluation.Refuse(PlayerRefusal.PantryEmpty, "The pantry is empty.");

        var groceryPantry = world.Get<HouseholdPantry>(owner);
        var required = grocery.PantryUnits * request.Quantity;
        if (grocery.Category != GroceryCategory.Household && groceryPantry.FoodUnits < required)
            return ActionEvaluation.Refuse(PlayerRefusal.PantryEmpty, "There is not enough food in the pantry.");

        return ActionEvaluation.Allow(plan: new ConsumeItemPlan(grocery.Id, request.Quantity, Grocery: true));
    }

    private ActionEvaluation EvaluateInvitePartner(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.InvitePartner>(payload, out var request, out var error))
            return RefuseMalformed(error);

        Entity target;
        if (!string.IsNullOrWhiteSpace(request!.TargetPlayerId))
        {
            if (!players.TryGet(request.TargetPlayerId, out var requestedProfile) ||
                requestedProfile.ControlledGenos == Entity.Null ||
                !world.IsAlive(requestedProfile.ControlledGenos))
                return ActionEvaluation.Refuse(PlayerRefusal.TargetNotFound, "That player is not available.");
            target = requestedProfile.ControlledGenos;
        }
        else if (!TryEntity(request.TargetGenosNetworkId, out target))
        {
            return ActionEvaluation.Refuse(PlayerRefusal.TargetNotFound, "That genos is not available.");
        }

        if (target == genos)
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "You cannot invite yourself.");
        if (world.Has<Dead>(target))
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "That genos is not available.");

        if (players.TryGetByGenos(target, out var targetProfile) &&
            !string.Equals(targetProfile.PlayerId, PlayerIdOf(genos), StringComparison.Ordinal))
        {
            var playerInvite = playerRelationships.EvaluateInvitePartner(PlayerIdOf(genos), targetProfile.PlayerId, tick);
            if (!playerInvite.Applied)
                return ActionEvaluation.RefuseRuntime(playerInvite.Code, playerInvite.Message ?? "Not eligible.");

            return ActionEvaluation.Allow(
                durationTicks: InvitationLifetimeTicks,
                targetHint: Hint("player", 0),
                plan: new InvitePartnerPlan(target, targetProfile.PlayerId, OtherPlayer: true));
        }

        var eligibility = EligibilityPolicy.Evaluate(
            world,
            relationships,
            genos,
            target,
            relationshipOptions,
            (first, second) => InConsentCooldown(second, first, tick));
        if (!eligibility.Eligible)
            return ActionEvaluation.RefuseRuntime(eligibility.Code, eligibility.Reason ?? "Not eligible.");

        if (HasPendingInvitation(genos, target))
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "An invitation is already pending.");

        return ActionEvaluation.Allow(
            durationTicks: InvitationLifetimeTicks,
            targetHint: Hint("genos", NetworkIdOf(target)),
            plan: new InvitePartnerPlan(target, null, OtherPlayer: false));
    }

    private ActionEvaluation EvaluateInviteCohabit(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.InviteCohabit>(payload, out var request, out var error))
            return RefuseMalformed(error);

        var targetPlayerId = request!.TargetPlayerId?.Trim();
        if (string.IsNullOrWhiteSpace(targetPlayerId))
            return ActionEvaluation.Refuse(PlayerRefusal.TargetRequired, "Choose a partner to cohabit with.");

        var playerId = PlayerIdOf(genos);
        if (string.Equals(targetPlayerId, playerId, StringComparison.Ordinal))
            return ActionEvaluation.Refuse(PlayerRefusal.SelfTarget, "You cannot invite yourself.");

        var evaluation = playerRelationships.EvaluateInviteCohabit(playerId, targetPlayerId, tick);
        if (!evaluation.Applied)
            return ActionEvaluation.RefuseRuntime(evaluation.Code, evaluation.Message ?? "Not available.");

        return ActionEvaluation.Allow(
            durationTicks: InvitationLifetimeTicks,
            targetHint: Hint("player", 0),
            plan: new InviteCohabitPlan(targetPlayerId));
    }

    private ActionEvaluation EvaluateEndPartnership(Entity genos, JsonElement payload)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.EndPartnership>(payload, out var request, out var error))
            return RefuseMalformed(error);

        var partnerPlayerId = request!.PartnerPlayerId?.Trim();
        if (string.IsNullOrWhiteSpace(partnerPlayerId))
            return ActionEvaluation.Refuse(PlayerRefusal.TargetRequired, "Choose the partner to end the partnership with.");

        var evaluation = playerRelationships.EvaluateEndPartnership(PlayerIdOf(genos), partnerPlayerId);
        if (!evaluation.Applied)
            return ActionEvaluation.RefuseRuntime(evaluation.Code, evaluation.Message ?? "Not available.");

        return ActionEvaluation.Allow(plan: new EndPartnershipPlan(partnerPlayerId, request.SplitHousehold));
    }

    private ActionEvaluation EvaluatePerformVenueActivity(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.PerformVenueActivity>(payload, out var request, out var error))
            return RefuseMalformed(error);

        if (!Enum.TryParse<VenueActivity>(request!.Activity, ignoreCase: true, out var activity))
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidActivity, "Unknown venue activity.");

        var playerId = PlayerIdOf(genos);
        var venue = venues.VenueOf(playerId);
        if (venue == Entity.Null)
            return ActionEvaluation.Refuse(PlayerRefusal.NotInVenue, "Enter a social venue first.");

        var evaluation = venueActivities.Evaluate(playerId, venue, activity, tick);
        if (!evaluation.Applied)
            return ActionEvaluation.RefuseRuntime(evaluation.Code, evaluation.Message ?? "Activity not allowed.");

        return ActionEvaluation.Allow(
            targetHint: Hint("venue", NetworkIdOf(venue)),
            plan: new VenueActivityPlan(venue, activity));
    }

    private ActionEvaluation EvaluateRespondToInvitation(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.RespondToInvitation>(payload, out var request, out var error))
            return RefuseMalformed(error);

        if (TryFindInvitation(request!.InvitationId, out var entity))
        {
            var invitation = world.Get<FamilyInvitation>(entity);
            if (invitation.To != genos)
                return ActionEvaluation.Refuse(PlayerRefusal.NotTarget, "That invitation was not sent to you.");
            if (invitation.Status != FamilyInvitationStatus.Pending)
                return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "That invitation is no longer pending.");
            if (tick > invitation.ExpiresTick)
                return ActionEvaluation.Refuse(PlayerRefusal.OfferExpired, "That invitation has expired.");

            var carrying = Entity.Null;
            var other = Entity.Null;
            if (request.Accept)
            {
                if (invitation.Kind == FamilyInvitationKind.Conception)
                {
                    carrying = ChooseCarryingParent(invitation.From, invitation.To);
                    if (carrying == Entity.Null)
                        return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "This pair cannot conceive under the current policy.");
                    other = carrying == invitation.From ? invitation.To : invitation.From;
                    if (!CanConceive(carrying, other, tick, out var reason))
                        return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, reason);
                }
                else if (invitation.Kind != FamilyInvitationKind.Partnership)
                {
                    return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "That invitation kind is not supported yet.");
                }
            }

            return ActionEvaluation.Allow(
                durationTicks: Math.Max(0, invitation.ExpiresTick - tick),
                targetHint: Hint("invitation", 0),
                plan: new RespondToInvitationPlan(entity, null, request.Accept, carrying, other));
        }

        var playerInvitation = playerRelationships.FindInvitation(request.InvitationId, tick);
        if (playerInvitation is null)
            return ActionEvaluation.Refuse(PlayerRefusal.OfferNotFound, "That invitation no longer exists.");
        if (!string.Equals(playerInvitation.ToPlayerId, PlayerIdOf(genos), StringComparison.Ordinal))
            return ActionEvaluation.Refuse(PlayerRefusal.NotTarget, "That invitation was not sent to you.");
        if (playerInvitation.Status == FamilyInvitationStatus.Expired)
            return ActionEvaluation.Refuse(PlayerRefusal.OfferExpired, "That invitation has expired.");
        if (playerInvitation.Status != FamilyInvitationStatus.Pending)
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "That invitation is no longer pending.");
        if (tick > playerInvitation.ExpiresTick)
            return ActionEvaluation.Refuse(PlayerRefusal.OfferExpired, "That invitation has expired.");

        return ActionEvaluation.Allow(
            durationTicks: Math.Max(0, playerInvitation.ExpiresTick - tick),
            targetHint: Hint("invitation", 0),
            plan: new RespondToInvitationPlan(Entity.Null, playerInvitation, request.Accept, Entity.Null, Entity.Null));
    }

    private ActionEvaluation EvaluateTryForChild(Entity genos, JsonElement payload, long tick)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.TryForChild>(payload, out var request, out var error))
            return RefuseMalformed(error);
        if (!TryEntity(request!.PartnerGenosNetworkId, out var partner))
            return ActionEvaluation.Refuse(PlayerRefusal.TargetNotFound, "That genos is not available.");
        if (!relationships.PartnersOf(genos).Contains(partner) || !relationships.PartnersOf(partner).Contains(genos))
            return ActionEvaluation.Refuse(PlayerRefusal.NotPartner, "You need an active partnership first.");

        var playerId = PlayerIdOf(genos);
        var partnerIsOtherPlayer = players.TryGetByGenos(partner, out var partnerProfile) &&
                                   !string.Equals(partnerProfile.PlayerId, playerId, StringComparison.Ordinal);
        if (partnerIsOtherPlayer)
        {
            if (HasPendingInvitation(genos, partner, FamilyInvitationKind.Conception))
                return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "A conception invitation is already pending.");

            return ActionEvaluation.Allow(
                durationTicks: InvitationLifetimeTicks,
                targetHint: Hint("genos", NetworkIdOf(partner)),
                plan: new TryForChildPlan(partner, Entity.Null, Entity.Null, OtherPlayer: true));
        }

        var carrying = ChooseCarryingParent(genos, partner);
        if (carrying == Entity.Null)
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "This pair cannot conceive under the current policy.");
        var other = carrying == genos ? partner : genos;
        if (!CanConceive(carrying, other, tick, out var reason))
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, reason);

        return ActionEvaluation.Allow(
            targetHint: Hint("genos", NetworkIdOf(partner)),
            plan: new TryForChildPlan(partner, carrying, other, OtherPlayer: false));
    }

    private ActionEvaluation EvaluateNameChild(Entity genos, JsonElement payload)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.NameChild>(payload, out var request, out var error))
            return RefuseMalformed(error);
        if (!TryEntity(request!.ChildNetworkId, out var child) || !world.Has<GenName>(child))
            return ActionEvaluation.Refuse(PlayerRefusal.TargetNotFound, "That child is not available.");
        if (world.Has<Dead>(child))
            return ActionEvaluation.Refuse(PlayerRefusal.NotAvailable, "That child is not available.");

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 40)
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "A name between 1 and 40 characters is required.");

        var isParent = relationships.IsParentOf(genos, child);
        var household = LifeSystemHelpers.HouseholdOf(world, economy, child);
        var sameHousehold = household != Entity.Null &&
                            LifeSystemHelpers.HouseholdOf(world, economy, genos) == household;
        if (!isParent && !sameHousehold)
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "You can only name your own child.");

        return ActionEvaluation.Allow(
            targetHint: Hint("genos", NetworkIdOf(child)),
            plan: new NameChildPlan(child, name));
    }

    private ActionEvaluation EvaluateChooseContinuation(JsonElement payload)
    {
        if (!PlayerActionPayloads.TryParse<PlayerActionPayloads.ChooseContinuation>(payload, out var request, out var error))
            return RefuseMalformed(error);
        if (!Enum.TryParse<PlayerContinuationPolicy>(request!.Policy, ignoreCase: true, out var policy))
            return ActionEvaluation.Refuse(PlayerRefusal.InvalidState, "Unknown continuation policy.");

        return ActionEvaluation.Allow(plan: new ContinuationPlan(policy));
    }

    // ---------------------------------------------------------------- apply phase

    private PlayerActionResult ApplyForJob(Entity genos, ActionEvaluation evaluation, long tick)
    {
        var plan = (JobApplicationPlan)evaluation.Plan!;
        var offer = new PlayerJobOffer
        {
            OfferId = Guid.NewGuid(),
            Posting = plan.Posting,
            Office = plan.Office,
            Position = plan.PostingData.Position,
            SalaryPerCycle = plan.PostingData.SalaryPerCycle,
            CreatedTick = tick,
            ExpiresTick = plan.ExpiresTick
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

    private PlayerActionResult ApplyJobOffer(Entity genos, ActionEvaluation evaluation, long tick)
    {
        var offer = ((JobOfferPlan)evaluation.Plan!).Offer;
        if (!hiring.TryHireApplicant(genos, offer.Posting, tick, out var hireError))
            return PlayerActionResult.Reject(PlayerRefusal.NotAvailable, hireError ?? "The posting is no longer available.");

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

    private PlayerActionResult ApplyQuitJob(Entity genos, ActionEvaluation evaluation, long tick)
    {
        var plan = (QuitJobPlan)evaluation.Plan!;
        var employee = world.Get<Employee>(genos);
        LifeSystemHelpers.RemoveEmployment(world, economy.Employment, economy, genos, tick);
        if (world.Has<PlayerEmployment>(genos))
            world.Remove<PlayerEmployment>(genos);
        if (world.Has<PlayerJobOffer>(genos))
            world.Remove<PlayerJobOffer>(genos);

        economy.Emit(new PlayerQuitJob(PlayerIdOf(genos), NetworkIdOf(genos), NetworkIdOf(employee.Company), plan.Reason, tick));
        return PlayerActionResult.Success("You quit your job.");
    }

    private PlayerActionResult ApplyRequestHousing(Entity genos, ActionEvaluation evaluation, long tick)
    {
        var plan = (HousingOfferPlan)evaluation.Plan!;
        var offer = new PlayerLeaseOffer
        {
            OfferId = Guid.NewGuid(),
            Unit = plan.Unit,
            Property = plan.Property,
            MonthlyRent = plan.Rent,
            CreatedTick = tick,
            ExpiresTick = plan.ExpiresTick
        };
        if (world.Has<PlayerLeaseOffer>(genos))
            world.Get<PlayerLeaseOffer>(genos) = offer;
        else
            world.Add(genos, offer);

        return PlayerActionResult.Success("Lease offer created.", new
        {
            offer.OfferId,
            UnitNetworkId = NetworkIdOf(plan.Unit),
            RentCents = plan.Rent.Cents,
            offer.ExpiresTick
        });
    }

    private PlayerActionResult ApplyAcceptLease(string playerId, Entity genos, ActionEvaluation evaluation, long tick)
    {
        var offer = ((LeaseOfferPlan)evaluation.Plan!).Offer;
        var unit = offer.Unit;

        var household = LifeSystemHelpers.HouseholdOf(world, economy, genos);
        if (household == Entity.Null)
        {
            var householdId = households.Create(NetworkIdOf(genos), tick);
            if (!households.TryGet(householdId, out household))
                return PlayerActionResult.Reject(PlayerRefusal.InvalidState, "A household could not be created.");
        }

        if (!allocator.TryAssign(household, unit, Tenure.Renter, tick, rentOverride: offer.MonthlyRent))
            return PlayerActionResult.Reject(PlayerRefusal.NotAvailable, "The unit could not be assigned.");

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

    private PlayerActionResult ApplyMoveOut(Entity genos, ActionEvaluation evaluation, long tick)
    {
        var plan = (MoveOutPlan)evaluation.Plan!;
        var household = plan.Household;
        var current = economy.Housing.UnitOf(household);
        var destination = plan.Destination;
        var tenure = plan.Sheltered ? Tenure.Sheltered : Tenure.Renter;
        if (!allocator.TryAssign(household, destination, tenure, tick, reference: "housing.move"))
            return PlayerActionResult.Reject(PlayerRefusal.NotAvailable, "The move could not be completed.");

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

    private PlayerActionResult ApplyPayArrears(Entity genos, ActionEvaluation evaluation, long tick)
    {
        var plan = (ArrearsPlan)evaluation.Plan!;
        var household = plan.Household;

        if (!TryDebitPlayer(genos, plan.Amount, LedgerKind.RentArrears, tick, "player.arrears"))
            return PlayerActionResult.Reject(PlayerRefusal.InsufficientFunds, "You cannot afford that payment.");

        var data = world.Get<Household>(household);
        data.RentArrears -= plan.RentPaid;
        data.UtilityArrears -= plan.UtilityPaid;
        data.NoUtilities = data.UtilityArrears.IsPositive;
        if (data.RentArrears.IsZero)
        {
            data.ArrearsCycles = 0;
            data.NoticeStage = 0;
            data.NoticeStartedTick = 0;
            data.CureDeadlineTick = 0;
        }
        world.Get<Household>(household) = data;

        CreditLandlord(household, plan.RentPaid, tick);
        economy.Government.Balance += plan.UtilityPaid;

        economy.Emit(new PlayerArrearsPaid(NetworkIdOf(genos), NetworkIdOf(household), plan.Amount.Cents, tick));
        return PlayerActionResult.Success("Arrears paid.", new { AmountCents = plan.Amount.Cents });
    }

    private PlayerActionResult ApplyBuyItem(Entity genos, ActionEvaluation evaluation, long tick)
    {
        var plan = (BuyItemPlan)evaluation.Plan!;
        if (plan.Grocery)
            return ApplyBuyGroceryItem(genos, plan, tick);

        if (!PlayerCatalog.TryGet(plan.ItemId, out var item))
            return PlayerActionResult.Reject(PlayerRefusal.NotAvailable, "That item is not sold here.");

        if (!TryDebitPlayer(genos, plan.Total, LedgerKind.ConsumerSpending, tick, "player.shop"))
            return PlayerActionResult.Reject(PlayerRefusal.InsufficientFunds, "You cannot afford that purchase.");

        CreditSeller(plan.Seller, plan.Store, plan.Total, "Grocery", tick);

        var pantryOwner = PantryOwner(genos);
        var pantry = world.Has<HouseholdPantry>(pantryOwner)
            ? world.Get<HouseholdPantry>(pantryOwner)
            : new HouseholdPantry();
        pantry.FoodUnits += item.FoodUnits * plan.Quantity;
        pantry.HouseholdGoods += item.HouseholdGoods * plan.Quantity;
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
            NetworkIdOf(plan.Store),
            item.Id,
            plan.Quantity,
            plan.Total.Cents,
            tick));

        return PlayerActionResult.Success("Purchase complete.", new
        {
            item.Id,
            plan.Quantity,
            AmountCents = plan.Total.Cents,
            pantry.FoodUnits,
            pantry.HouseholdGoods
        });
    }

    private PlayerActionResult ApplyBuyGroceryItem(Entity genos, BuyItemPlan plan, long tick)
    {
        groceryStores.EnsureInventory(plan.Store, tick);
        var index = GroceryCatalog.IndexOf(plan.ItemId);
        if (!groceryStores.TrySell(plan.Store, index, plan.Quantity, out var total))
            return PlayerActionResult.Reject(PlayerRefusal.OutOfStock, "There is not enough stock of that item.");

        if (!TryDebitPlayer(genos, total, LedgerKind.ConsumerSpending, tick, "player.grocery", NetworkIdOf(plan.Store)))
            return PlayerActionResult.Reject(PlayerRefusal.InsufficientFunds, "You cannot afford that purchase.");

        CreditSeller(plan.Seller, plan.Store, total, "Grocery", tick);

        var pantryOwner = PantryOwner(genos);
        var pantry = world.Has<HouseholdPantry>(pantryOwner)
            ? world.Get<HouseholdPantry>(pantryOwner)
            : new HouseholdPantry();
        var item = GroceryCatalog.Items[index];
        pantry.FoodUnits += item.PantryUnits * plan.Quantity;
        pantry.LastPurchaseTick = tick;
        StorePantry(pantryOwner, pantry);

        var state = world.Has<ConsumptionState>(genos) ? world.Get<ConsumptionState>(genos) : new ConsumptionState();
        state.FoodPurchased = true;
        if (world.Has<ConsumptionState>(genos))
            world.Get<ConsumptionState>(genos) = state;
        else
            world.Add(genos, state);

        var nutrition = world.Has<NutritionState>(genos) ? world.Get<NutritionState>(genos) : new NutritionState();
        nutrition.Balance = Math.Clamp(nutrition.Balance + item.Nutrition * plan.Quantity * 2, -100, 100);
        nutrition.LastMealTick = tick;
        if (world.Has<NutritionState>(genos))
            world.Get<NutritionState>(genos) = nutrition;
        else
            world.Add(genos, nutrition);

        economy.Emit(new PlayerBoughtItem(
            NetworkIdOf(genos),
            NetworkIdOf(plan.Store),
            item.Id,
            plan.Quantity,
            total.Cents,
            tick));
        economy.Emit(new GroceryPurchase(
            NetworkIdOf(PantryOwner(genos)),
            NetworkIdOf(plan.Store),
            item.Id,
            plan.Quantity,
            total.Cents,
            tick));

        return PlayerActionResult.Success("Purchase complete.", new
        {
            item.Id,
            plan.Quantity,
            AmountCents = total.Cents,
            pantry.FoodUnits
        });
    }

    private PlayerActionResult ApplyConsumeItem(Entity genos, ActionEvaluation evaluation, long tick)
    {
        var plan = (ConsumeItemPlan)evaluation.Plan!;
        return plan.Grocery
            ? ApplyConsumeGroceryItem(genos, plan, tick)
            : ApplyConsumeCatalogItem(genos, plan, tick);
    }

    private PlayerActionResult ApplyConsumeCatalogItem(Entity genos, ConsumeItemPlan plan, long tick)
    {
        if (!PlayerCatalog.TryGet(plan.ItemId, out var item))
            return PlayerActionResult.Reject(PlayerRefusal.NotAvailable, "That item is not in the pantry.");

        var pantryOwner = PantryOwner(genos);
        var pantry = world.Get<HouseholdPantry>(pantryOwner);
        pantry.FoodUnits = Math.Max(0, pantry.FoodUnits - item.FoodUnits * plan.Quantity);
        pantry.HouseholdGoods = Math.Max(0, pantry.HouseholdGoods - item.HouseholdGoods * plan.Quantity);
        StorePantry(pantryOwner, pantry);

        if (world.Has<Needs>(genos))
        {
            var needs = world.Get<Needs>(genos);
            needs.Hunger = Math.Min(100f, needs.Hunger + item.Hunger * plan.Quantity);
            needs.Hygiene = Math.Min(100f, needs.Hygiene + item.Hygiene * plan.Quantity);
            world.Get<Needs>(genos) = needs;
        }

        economy.Emit(new PlayerConsumedItem(NetworkIdOf(genos), item.Id, plan.Quantity, tick));
        return PlayerActionResult.Success("Item consumed.", new { item.Id, plan.Quantity });
    }

    private PlayerActionResult ApplyConsumeGroceryItem(Entity genos, ConsumeItemPlan plan, long tick)
    {
        if (!GroceryCatalog.TryGet(plan.ItemId, out var item))
            return PlayerActionResult.Reject(PlayerRefusal.NotAvailable, "That item is not in the pantry.");

        var pantryOwner = PantryOwner(genos);
        var pantry = world.Get<HouseholdPantry>(pantryOwner);
        var required = item.PantryUnits * plan.Quantity;
        if (item.Category != GroceryCategory.Household)
            pantry.FoodUnits = Math.Max(0, pantry.FoodUnits - required);
        StorePantry(pantryOwner, pantry);

        if (world.Has<Needs>(genos))
        {
            var needs = world.Get<Needs>(genos);
            needs.Hunger = Math.Min(100f, needs.Hunger + item.Nutrition * 8f * plan.Quantity);
            if (item.Category == GroceryCategory.Household)
                needs.Hygiene = Math.Min(100f, needs.Hygiene + 25f * plan.Quantity);
            world.Get<Needs>(genos) = needs;
        }

        var nutrition = world.Has<NutritionState>(genos) ? world.Get<NutritionState>(genos) : new NutritionState();
        var balanced = item.Category is GroceryCategory.Staple or GroceryCategory.Produce or GroceryCategory.Protein;
        nutrition.Balance = Math.Clamp(nutrition.Balance + (balanced ? 8 : 2) * plan.Quantity, -100, 100);
        nutrition.LastMealTick = tick;
        if (nutrition.Balance < 0) nutrition.ConsecutivePoorDays++;
        else nutrition.ConsecutivePoorDays = 0;
        if (world.Has<NutritionState>(genos))
            world.Get<NutritionState>(genos) = nutrition;
        else
            world.Add(genos, nutrition);

        economy.Emit(new PlayerConsumedItem(NetworkIdOf(genos), item.Id, plan.Quantity, tick));
        return PlayerActionResult.Success("Item consumed.", new { item.Id, plan.Quantity });
    }

    private PlayerActionResult ApplyInvitePartner(string playerId, Entity genos, ActionEvaluation evaluation, long tick)
    {
        var plan = (InvitePartnerPlan)evaluation.Plan!;
        if (plan.OtherPlayer)
            return MapRelationshipResult(playerRelationships.InvitePartner(playerId, plan.TargetPlayerId!, tick));

        var target = plan.Target;
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
        return PlayerActionResult.Reject(PlayerRefusal.Declined, consent.Message);
    }

    private PlayerActionResult ApplyInviteCohabit(string playerId, Entity genos, ActionEvaluation evaluation, long tick)
    {
        var plan = (InviteCohabitPlan)evaluation.Plan!;
        return MapRelationshipResult(playerRelationships.InviteCohabit(playerId, plan.TargetPlayerId, tick));
    }

    private PlayerActionResult ApplyEndPartnership(string playerId, Entity genos, ActionEvaluation evaluation, long tick)
    {
        var plan = (EndPartnershipPlan)evaluation.Plan!;
        return MapRelationshipResult(playerRelationships.EndPartnership(
            playerId,
            plan.PartnerPlayerId,
            tick,
            plan.SplitHousehold));
    }

    private PlayerActionResult ApplyPerformVenueActivity(string playerId, Entity genos, ActionEvaluation evaluation, long tick)
    {
        var plan = (VenueActivityPlan)evaluation.Plan!;
        var result = venueActivities.TryPerform(playerId, plan.Venue, plan.Activity, tick);
        if (!result.Applied)
            return RejectRuntimeCode(result.Code, result.Message, "Activity not allowed.");

        return PlayerActionResult.Success(
            "Activity performed.",
            new
            {
                venueNetworkId = NetworkIdOf(plan.Venue),
                activity = plan.Activity.ToString(),
                recipients = result.Recipients
            });
    }

    private PlayerActionResult ApplyRespondToInvitation(string playerId, Entity genos, ActionEvaluation evaluation, long tick)
    {
        var plan = (RespondToInvitationPlan)evaluation.Plan!;
        if (plan.PlayerInvitation is not null)
        {
            return MapRelationshipResult(playerRelationships.Respond(
                playerId,
                plan.PlayerInvitation.Id,
                plan.Accept,
                tick));
        }

        var entity = plan.InvitationEntity;
        var invitation = world.Get<FamilyInvitation>(entity);
        if (!plan.Accept)
        {
            invitation.Status = FamilyInvitationStatus.Declined;
            world.Get<FamilyInvitation>(entity) = invitation;
            SetConsentCooldown(invitation.From, invitation.To, tick);
            economy.Emit(new PlayerInvitationResponded(
                NetworkIdOf(invitation.From), NetworkIdOf(invitation.To), invitation.Id,
                invitation.Kind.ToString(), false, "declined", tick));
            return PlayerActionResult.Success("Invitation declined.");
        }

        if (invitation.Kind == FamilyInvitationKind.Conception)
        {
            var conception = TryConceiveAfterEvaluation(plan.Carrying, plan.Other, tick);
            if (!conception.Applied)
                return conception;
        }
        else if (!AcceptPartnership(invitation.From, invitation.To, tick, 0))
        {
            return PlayerActionResult.Reject(PlayerRefusal.InvalidState, "The partnership could not be formed.");
        }

        invitation.Status = FamilyInvitationStatus.Accepted;
        world.Get<FamilyInvitation>(entity) = invitation;
        economy.Emit(new PlayerInvitationResponded(
            NetworkIdOf(invitation.From), NetworkIdOf(invitation.To), invitation.Id,
            invitation.Kind.ToString(), true, null, tick));
        return PlayerActionResult.Success("Invitation accepted.");
    }

    private PlayerActionResult ApplyTryForChild(Entity genos, ActionEvaluation evaluation, long tick)
    {
        var plan = (TryForChildPlan)evaluation.Plan!;
        if (!plan.OtherPlayer)
            return TryConceiveAfterEvaluation(plan.Carrying, plan.Other, tick);

        var partner = plan.Partner;
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

    private PlayerActionResult ApplyNameChild(Entity genos, ActionEvaluation evaluation, long tick)
    {
        var plan = (NameChildPlan)evaluation.Plan!;
        world.Get<GenName>(plan.Child) = new GenName(plan.Name);
        economy.Emit(new PlayerChildNamed(NetworkIdOf(genos), NetworkIdOf(plan.Child), plan.Name, tick));
        return PlayerActionResult.Success("Child named.", new { name = plan.Name });
    }

    private PlayerActionResult ApplyChooseContinuation(string playerId, Entity genos, ActionEvaluation evaluation, long tick)
    {
        var plan = (ContinuationPlan)evaluation.Plan!;
        var profile = players.GetOrCreate(playerId, genos, tick);
        profile.ContinuationPolicy = plan.Policy;
        profile.LastActionTick = tick;
        return PlayerActionResult.Success("Continuation preference saved.", new { policy = plan.Policy.ToString() });
    }

    // ---------------------------------------------------------------- plans

    private readonly record struct JobApplicationPlan(
        Entity Office,
        Entity Posting,
        JobPosting PostingData,
        long ExpiresTick);

    private readonly record struct JobOfferPlan(PlayerJobOffer Offer);

    private readonly record struct QuitJobPlan(string Reason);

    private readonly record struct HousingOfferPlan(
        Entity Unit,
        Entity Property,
        Money Rent,
        long ExpiresTick);

    private readonly record struct LeaseOfferPlan(PlayerLeaseOffer Offer);

    private readonly record struct MoveOutPlan(Entity Household, Entity Destination, bool Sheltered);

    private readonly record struct ArrearsPlan(
        Entity Household,
        Money Amount,
        Money RentPaid,
        Money UtilityPaid);

    private readonly record struct BuyItemPlan(
        Entity Store,
        Entity Seller,
        string ItemId,
        int Quantity,
        Money Total,
        bool Grocery);

    private readonly record struct ConsumeItemPlan(string ItemId, int Quantity, bool Grocery);

    private readonly record struct InvitePartnerPlan(Entity Target, string? TargetPlayerId, bool OtherPlayer);

    private readonly record struct InviteCohabitPlan(string TargetPlayerId);

    private readonly record struct EndPartnershipPlan(string PartnerPlayerId, bool SplitHousehold);

    private readonly record struct VenueActivityPlan(Entity Venue, VenueActivity Activity);

    private readonly record struct RespondToInvitationPlan(
        Entity InvitationEntity,
        PlayerRelationshipInvitation? PlayerInvitation,
        bool Accept,
        Entity Carrying,
        Entity Other);

    private readonly record struct TryForChildPlan(Entity Partner, Entity Carrying, Entity Other, bool OtherPlayer);

    private readonly record struct NameChildPlan(Entity Child, string Name);

    private readonly record struct ContinuationPlan(PlayerContinuationPolicy Policy);

    // ---------------------------------------------------------------- evaluation helpers

    private static ActionEvaluation RefuseMalformed(string? error) =>
        ActionEvaluation.Refuse(PlayerRefusal.MalformedPayload, error ?? "The payload could not be read.");

    private bool CanAfford(Entity genos, Money amount)
    {
        var wallet = world.Has<Wallet>(genos) ? world.Get<Wallet>(genos).Balance : Money.Zero;
        var savings = world.Has<SavingsAccount>(genos) ? world.Get<SavingsAccount>(genos).Balance : Money.Zero;
        return wallet + savings >= amount;
    }

    private bool HasOpenSeat(Entity posting)
    {
        var found = false;
        world.Query(in new QueryDescription().WithAll<OpenPosition>(),
            (Entity entity, ref OpenPosition open) =>
            {
                if (!found && open.Posting == posting)
                    found = true;
            });
        return found;
    }

    private static string Hint(string kind, ulong networkId) =>
        networkId == 0
            ? kind
            : string.Create(CultureInfo.InvariantCulture, $"{kind}:{networkId}");

    /// <summary>
    /// Travel time from the pathfinder, in simulation ticks: path length divided by the player's
    /// tiles-per-tick speed. A world without a map (tests, headless fixtures) reports no travel.
    /// </summary>
    private long TravelTicks(Entity genos, Entity building)
    {
        if (map is null || !world.IsAlive(genos) || !world.Has<TilePosition>(genos) || !world.Has<Placement>(building))
            return 0;

        var start = world.Get<TilePosition>(genos).Tile;
        var target = world.Get<Placement>(building).Entry;
        var path = TilePathfinder.FindPath(map, start, target, 4096, out _);
        if (path is null)
            return 0;

        var tilesPerTick = MovementOptions.Default.PlayerSpeedTilesPerSecond / SimulationLoop.TicksPerSecond;
        return tilesPerTick <= 0
            ? 0
            : (long)Math.Ceiling(path.Count / (double)tilesPerTick);
    }

    private static readonly JsonSerializerOptions CatalogueJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// The server's own context: what it already knows about this player. The dial may always send
    /// richer targets (the listing it is looking at, the item it offers); without them the catalogue
    /// still evaluates the player's own offers, invitations and partner rather than withholding a
    /// verb for lack of a body.
    /// </summary>
    private IReadOnlyList<ActionCatalogueTarget> DefaultCatalogueTargets(Entity genos, long tick)
    {
        var targets = new List<ActionCatalogueTarget>();

        void Add(PlayerActionKind kind, object payload) =>
            targets.Add(new ActionCatalogueTarget(
                kind,
                JsonSerializer.SerializeToElement(payload, payload.GetType(), CatalogueJsonOptions)));

        var playerId = PlayerIdOf(genos);

        if (world.Has<PlayerJobOffer>(genos))
            Add(PlayerActionKind.AcceptJobOffer, new PlayerActionPayloads.AcceptJobOffer(world.Get<PlayerJobOffer>(genos).OfferId));
        if (world.Has<PlayerLeaseOffer>(genos))
            Add(PlayerActionKind.AcceptLease, new PlayerActionPayloads.AcceptLease(world.Get<PlayerLeaseOffer>(genos).OfferId));

        Add(PlayerActionKind.MoveOut, new PlayerActionPayloads.MoveOut(0));
        Add(PlayerActionKind.PayArrears, new PlayerActionPayloads.PayArrears(0, false));

        var invitationId = PendingInvitationIdFor(genos, playerId, tick);
        if (invitationId is { } id)
            Add(PlayerActionKind.RespondToInvitation, new PlayerActionPayloads.RespondToInvitation(id, true));

        var partner = relationships.PartnersOf(genos).FirstOrDefault();
        if (partner != Entity.Null)
        {
            Add(PlayerActionKind.TryForChild, new PlayerActionPayloads.TryForChild(NetworkIdOf(partner)));
            Add(PlayerActionKind.InvitePartner, new PlayerActionPayloads.InvitePartner(NetworkIdOf(partner), null));
            if (players.TryGetByGenos(partner, out var partnerProfile))
            {
                Add(PlayerActionKind.InviteCohabit, new PlayerActionPayloads.InviteCohabit(partnerProfile.PlayerId));
                Add(PlayerActionKind.EndPartnership, new PlayerActionPayloads.EndPartnership(partnerProfile.PlayerId, true));
            }
        }

        if (players.TryGet(playerId, out var profile))
            Add(PlayerActionKind.ChooseContinuation, new PlayerActionPayloads.ChooseContinuation(profile.ContinuationPolicy.ToString()));

        var venue = venues.VenueOf(playerId);
        if (venue != Entity.Null)
            Add(PlayerActionKind.PerformVenueActivity, new PlayerActionPayloads.PerformVenueActivity(VenueActivity.Talk.ToString()));

        if (world.Has<TilePosition>(genos))
        {
            var office = NearestOffice(genos);
            if (office != Entity.Null)
                Add(PlayerActionKind.ApplyForJob, new PlayerActionPayloads.ApplyForJob(0, NetworkIdOf(office)));

            var store = NearestOffice(genos);
            if (store != Entity.Null)
                Add(PlayerActionKind.BuyItem, new PlayerActionPayloads.BuyItem(NetworkIdOf(store), PlayerCatalog.FoodId, 1));
        }

        return targets;
    }

    private Guid? PendingInvitationIdFor(Entity genos, string playerId, long tick)
    {
        var family = Guid.Empty;
        world.Query(in new QueryDescription().WithAll<FamilyInvitation>(),
            (ref FamilyInvitation invitation) =>
            {
                if (family != Guid.Empty || invitation.To != genos ||
                    invitation.Status != FamilyInvitationStatus.Pending || tick > invitation.ExpiresTick)
                    return;
                family = invitation.Id;
            });
        if (family != Guid.Empty)
            return family;

        var player = playerRelationships.PendingInvitations(playerId, tick)
            .FirstOrDefault(invitation =>
                string.Equals(invitation.ToPlayerId, playerId, StringComparison.Ordinal) &&
                invitation.Status == FamilyInvitationStatus.Pending);
        return player?.Id;
    }

    private Entity NearestOffice(Entity genos)
    {
        if (!world.Has<TilePosition>(genos))
            return Entity.Null;

        var origin = world.Get<TilePosition>(genos).Tile;
        var best = Entity.Null;
        var bestDistance = int.MaxValue;
        world.Query(in new QueryDescription().WithAll<Office, Placement>(),
            (Entity entity, ref Office _, ref Placement placement) =>
            {
                var distance = origin.ChebyshevDistanceTo(placement.Entry);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = entity;
                }
            });
        return best;
    }
}