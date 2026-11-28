using System.Text.Json;
using Arch.Core;
using Genelife.Api.Commands;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Consumption;
using Genelife.Components.Employment;
using Genelife.Components.Family;
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
using Genelife.Player;
using Genelife.Social;
using Genelife.Values;

namespace Genelife.UnitTests.Players;

/// <summary>
/// AC-4a (GDD chapter 07 §7.6): the action catalogue never offers a verb <c>Evaluate</c> would
/// refuse, nor withholds one it would accept — with the <b>reason</b>, not merely a boolean.
///
/// Two things are under test:
/// 1. parity — every catalogue entry is the <c>Evaluate</c> result for that kind, including the
///    refusalCode and the cost/duration facts, over an enumerated context matrix;
/// 2. the completeness floor — every member of <see cref="PlayerActionKind"/> appears in at least one
///    fixture, so adding an 18th kind fails here until its refusal cases are supplied.
/// </summary>
public sealed class ActionCatalogueTests
{
    private const long Tick = 100;

    private sealed record Fixture(
        string Label,
        PlayerActionKind Kind,
        Func<Scenario, JsonElement> Payload,
        Func<Scenario> Build,
        string? ExpectedRefusal);

    private static readonly IReadOnlyList<Fixture> Matrix = BuildMatrix();

    private static JsonElement Payload(object value) => JsonSerializer.SerializeToElement(value);

    [Fact]
    public void Every_PlayerActionKind_appears_in_the_fixture_matrix()
    {
        var covered = Matrix.Select(fixture => fixture.Kind).Distinct().OrderBy(kind => (byte)kind).ToArray();
        var declared = Enum.GetValues<PlayerActionKind>().OrderBy(kind => (byte)kind).ToArray();

        Assert.Equal(declared, covered);
    }

    [Fact]
    public void Catalogue_entry_equals_Evaluate_for_every_fixture()
    {
        foreach (var fixture in Matrix)
        {
            using var scenario = fixture.Build();
            var payload = fixture.Payload(scenario);
            var evaluation = scenario.Actions.Evaluate(scenario.Genos, fixture.Kind, payload, Tick);
            var catalogue = scenario.Actions.BuildCatalogue(
                "p1",
                new ActionCatalogueRequest(scenario.Get(scenario.Genos), Tick,
                    new[] { new ActionCatalogueTarget(fixture.Kind, payload) }),
                Tick);

            var entry = Assert.Single(catalogue.Entries.Where(candidate => candidate.Kind == fixture.Kind));

            Assert.Equal(evaluation.Allowed, entry.Available);
            Assert.Equal(evaluation.RefusalCode, entry.RefusalCode);
            Assert.Equal(evaluation.CostCents, entry.CostCents);
            Assert.Equal(evaluation.DurationTicks, entry.DurationTicks);
            Assert.Equal(evaluation.TargetHint, entry.TargetHint);
            Assert.Equal(Tick, entry.AsOfTick);

            if (fixture.ExpectedRefusal is null)
            {
                Assert.True(entry.Available, $"{fixture.Label}: expected available, got {entry.RefusalCode}");
                Assert.Null(entry.RefusalCode);
            }
            else
            {
                Assert.False(entry.Available, $"{fixture.Label}: expected {fixture.ExpectedRefusal}, was available");
                Assert.Equal(fixture.ExpectedRefusal, entry.RefusalCode);
            }
        }
    }

    [Fact]
    public void An_unavailable_verb_carries_a_code_the_dial_can_render()
    {
        foreach (var fixture in Matrix)
        {
            using var scenario = fixture.Build();
            var payload = fixture.Payload(scenario);
            var catalogue = scenario.Actions.BuildCatalogue(
                "p1",
                new ActionCatalogueRequest(scenario.Get(scenario.Genos), Tick,
                    new[] { new ActionCatalogueTarget(fixture.Kind, payload) }),
                Tick);

            var entry = Assert.Single(catalogue.Entries.Where(candidate => candidate.Kind == fixture.Kind));
            if (!entry.Available)
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.RefusalCode),
                    $"{fixture.Label}: an unavailable verb must carry a refusalCode, not just a boolean");
            }
        }
    }

    [Fact]
    public void The_envelope_carries_the_shape_and_refusal_versions()
    {
        using var scenario = new Scenario();
        var catalogue = scenario.Actions.BuildCatalogue(
            "p1",
            new ActionCatalogueRequest(scenario.Get(scenario.Genos), Tick),
            Tick);

        Assert.Equal(ActionCatalogueVocabulary.ShapeVersion, catalogue.ShapeVersion);
        Assert.Equal(PlayerRefusalVocabulary.RefusalEnumVersion, catalogue.RefusalEnumVersion);
        Assert.Equal("p1", catalogue.PlayerId);
        Assert.Equal(Tick, catalogue.AsOfTick);
        Assert.Equal(Enum.GetValues<PlayerActionKind>().Length, catalogue.Entries.Count);
        Assert.All(catalogue.Entries, entry => Assert.Equal(Tick, entry.AsOfTick));
    }

    [Fact]
    public void A_catalogue_for_another_players_genos_refuses_every_kind_as_not_owner()
    {
        using var scenario = new Scenario();
        var catalogue = scenario.Actions.BuildCatalogue(
            "p1",
            new ActionCatalogueRequest(scenario.Get(scenario.Partner), Tick),
            Tick);

        Assert.All(catalogue.Entries, entry =>
        {
            Assert.False(entry.Available);
            Assert.Equal(PlayerRefusal.NotOwner.WireCode(), entry.RefusalCode);
        });
    }

    private static IReadOnlyList<Fixture> BuildMatrix()
    {
        static Fixture F(
            string label,
            PlayerActionKind kind,
            Func<Scenario, JsonElement> payload,
            string? expectedRefusal,
            Func<Scenario>? build = null) =>
            new(label, kind, payload, build ?? (() => new Scenario()), expectedRefusal);

        return new List<Fixture>
        {
            // ---- ApplyForJob
            F("ApplyForJob/valid", PlayerActionKind.ApplyForJob,
                s => Payload(new { officeNetworkId = s.Get(s.Office), postingNetworkId = 0UL }),
                null),
            F("ApplyForJob/wrong-tile", PlayerActionKind.ApplyForJob,
                s => Payload(new { officeNetworkId = s.Get(s.Office), postingNetworkId = 0UL }),
                "out_of_range",
                () => { var s = new Scenario(); s.MoveFar(); return s; }),
            F("ApplyForJob/wrong-building", PlayerActionKind.ApplyForJob,
                _ => Payload(new { officeNetworkId = 999_999UL, postingNetworkId = 0UL }),
                "not_available"),
            F("ApplyForJob/already-employed", PlayerActionKind.ApplyForJob,
                s => Payload(new { officeNetworkId = s.Get(s.Office), postingNetworkId = 0UL }),
                "already_employed",
                () => { var s = new Scenario(); s.Employ(); return s; }),

            // ---- AcceptJobOffer
            F("AcceptJobOffer/valid", PlayerActionKind.AcceptJobOffer,
                s => Payload(new { offerId = s.World.Get<PlayerJobOffer>(s.Genos).OfferId }),
                null,
                () => { var s = new Scenario(); s.GiveJobOffer(Tick + 50); return s; }),
            F("AcceptJobOffer/none", PlayerActionKind.AcceptJobOffer,
                _ => Payload(new { offerId = Guid.Empty }),
                "offer_not_found"),
            F("AcceptJobOffer/expired", PlayerActionKind.AcceptJobOffer,
                s => Payload(new { offerId = s.World.Get<PlayerJobOffer>(s.Genos).OfferId }),
                "offer_expired",
                () => { var s = new Scenario(); s.GiveJobOffer(Tick - 1); return s; }),

            // ---- QuitJob
            F("QuitJob/valid", PlayerActionKind.QuitJob,
                _ => Payload(new { reason = (string?)null }),
                null,
                () => { var s = new Scenario(); s.Employ(); return s; }),
            F("QuitJob/not-employed", PlayerActionKind.QuitJob,
                _ => Payload(new { reason = (string?)null }),
                "not_employed"),

            // ---- RequestHousing
            F("RequestHousing/valid", PlayerActionKind.RequestHousing,
                s => Payload(new { unitNetworkId = s.Get(s.Unit), propertyNetworkId = 0UL }),
                null,
                () => { var s = new Scenario(); s.ListUnit(); s.StandAtProperty(); return s; }),
            F("RequestHousing/no-money", PlayerActionKind.RequestHousing,
                s => Payload(new { unitNetworkId = s.Get(s.Unit), propertyNetworkId = 0UL }),
                "insufficient_funds",
                () => { var s = new Scenario(); s.ListUnit(); s.StandAtProperty(); s.ZeroWallet(); return s; }),
            F("RequestHousing/wrong-tile", PlayerActionKind.RequestHousing,
                s => Payload(new { unitNetworkId = s.Get(s.Unit), propertyNetworkId = 0UL }),
                "out_of_range",
                () => { var s = new Scenario(); s.ListUnit(); s.MoveFar(); return s; }),

            // ---- AcceptLease
            F("AcceptLease/valid", PlayerActionKind.AcceptLease,
                s => Payload(new { offerId = s.World.Get<PlayerLeaseOffer>(s.Genos).OfferId }),
                null,
                () => { var s = new Scenario(); s.GiveLeaseOffer(Tick + 50); return s; }),
            F("AcceptLease/none", PlayerActionKind.AcceptLease,
                _ => Payload(new { offerId = Guid.Empty }),
                "offer_not_found"),

            // ---- MoveOut
            F("MoveOut/valid", PlayerActionKind.MoveOut,
                s => Payload(new { destinationUnitNetworkId = s.Get(s.UnitB) }),
                null,
                () => { var s = new Scenario(); s.HouseGenosIn(s.Unit); s.StandAtProperty(); return s; }),
            F("MoveOut/no-household", PlayerActionKind.MoveOut,
                _ => Payload(new { destinationUnitNetworkId = 0UL }),
                "no_household"),

            // ---- PayArrears
            F("PayArrears/valid", PlayerActionKind.PayArrears,
                _ => Payload(new { amountCents = 0L, rentOnly = false }),
                null,
                () => { var s = new Scenario(); s.WithArrears(); return s; }),
            F("PayArrears/no-money", PlayerActionKind.PayArrears,
                _ => Payload(new { amountCents = 0L, rentOnly = false }),
                "insufficient_funds",
                () => { var s = new Scenario(); s.WithArrears(); s.ZeroWallet(); return s; }),
            F("PayArrears/no-arrears", PlayerActionKind.PayArrears,
                _ => Payload(new { amountCents = 0L, rentOnly = false }),
                "invalid_state",
                () => { var s = new Scenario(); s.HouseGenosIn(s.Unit); return s; }),

            // ---- BuyItem
            F("BuyItem/valid", PlayerActionKind.BuyItem,
                s => Payload(new { storeNetworkId = s.Get(s.Office), catalogItemId = "food", quantity = 1 }),
                null),
            F("BuyItem/no-money", PlayerActionKind.BuyItem,
                s => Payload(new { storeNetworkId = s.Get(s.Office), catalogItemId = "food", quantity = 1 }),
                "insufficient_funds",
                () => { var s = new Scenario(); s.ZeroWallet(); return s; }),
            F("BuyItem/wrong-tile", PlayerActionKind.BuyItem,
                s => Payload(new { storeNetworkId = s.Get(s.Office), catalogItemId = "food", quantity = 1 }),
                "out_of_range",
                () => { var s = new Scenario(); s.MoveFar(); return s; }),
            F("BuyItem/no-stock", PlayerActionKind.BuyItem,
                s => Payload(new { storeNetworkId = s.Get(s.Office), catalogItemId = "bread", quantity = 1 }),
                "out_of_stock",
                () => { var s = new Scenario(); s.StockBread(0); return s; }),

            // ---- ConsumeItem
            F("ConsumeItem/valid", PlayerActionKind.ConsumeItem,
                _ => Payload(new { catalogItemId = "food", quantity = 1 }),
                null,
                () => { var s = new Scenario(); s.FillPantry(5); return s; }),
            F("ConsumeItem/empty-pantry", PlayerActionKind.ConsumeItem,
                _ => Payload(new { catalogItemId = "food", quantity = 1 }),
                "pantry_empty"),

            // ---- InvitePartner
            F("InvitePartner/valid", PlayerActionKind.InvitePartner,
                s => Payload(new { targetGenosNetworkId = s.Get(s.Partner), targetPlayerId = (string?)null }),
                null),
            F("InvitePartner/self", PlayerActionKind.InvitePartner,
                s => Payload(new { targetGenosNetworkId = s.Get(s.Genos), targetPlayerId = (string?)null }),
                "invalid_state"),
            F("InvitePartner/target-missing", PlayerActionKind.InvitePartner,
                _ => Payload(new { targetGenosNetworkId = 999_999UL, targetPlayerId = (string?)null }),
                "target_not_found"),

            // ---- InviteCohabit
            F("InviteCohabit/valid", PlayerActionKind.InviteCohabit,
                _ => Payload(new { targetPlayerId = "p2" }),
                null,
                () => { var s = new Scenario(); s.MakePartners(); return s; }),
            F("InviteCohabit/not-partner", PlayerActionKind.InviteCohabit,
                _ => Payload(new { targetPlayerId = "p2" }),
                "not_partner"),
            F("InviteCohabit/target-required", PlayerActionKind.InviteCohabit,
                _ => Payload(new { targetPlayerId = "" }),
                "target_required"),

            // ---- EndPartnership
            F("EndPartnership/valid", PlayerActionKind.EndPartnership,
                _ => Payload(new { partnerPlayerId = "p2", splitHousehold = false }),
                null,
                () => { var s = new Scenario(); s.MakePartners(); return s; }),
            F("EndPartnership/not-partner", PlayerActionKind.EndPartnership,
                _ => Payload(new { partnerPlayerId = "p2", splitHousehold = false }),
                "not_partner"),

            // ---- PerformVenueActivity
            F("PerformVenueActivity/valid", PlayerActionKind.PerformVenueActivity,
                _ => Payload(new { activity = "Talk" }),
                null,
                () => { var s = new Scenario(); s.EnterVenue(); return s; }),
            F("PerformVenueActivity/not-in-venue", PlayerActionKind.PerformVenueActivity,
                _ => Payload(new { activity = "Talk" }),
                "not_in_venue"),
            F("PerformVenueActivity/cooldown-active", PlayerActionKind.PerformVenueActivity,
                _ => Payload(new { activity = "Talk" }),
                "activity_cooldown",
                () =>
                {
                    var s = new Scenario();
                    s.EnterVenue();
                    s.ActivityOnce();
                    return s;
                }),

            // ---- RespondToInvitation
            F("RespondToInvitation/valid", PlayerActionKind.RespondToInvitation,
                _ => Payload(new { invitationId = Guid.Empty, accept = false }),
                null,
                () => { var s = new Scenario(); s.ReceiveInvitation(); return s; }),
            F("RespondToInvitation/not-target", PlayerActionKind.RespondToInvitation,
                _ => Payload(new { invitationId = Guid.Empty, accept = false }),
                "not_target",
                () => { var s = new Scenario(); s.ReceiveInvitation(toPartner: true); return s; }),
            F("RespondToInvitation/missing", PlayerActionKind.RespondToInvitation,
                _ => Payload(new { invitationId = Guid.Empty, accept = false }),
                "offer_not_found"),

            // ---- TryForChild
            F("TryForChild/valid", PlayerActionKind.TryForChild,
                s => Payload(new { partnerGenosNetworkId = s.Get(s.Partner) }),
                null,
                () => { var s = new Scenario(); s.MakePartners(); return s; }),
            F("TryForChild/not-partner", PlayerActionKind.TryForChild,
                s => Payload(new { partnerGenosNetworkId = s.Get(s.Partner) }),
                "not_partner"),

            // ---- NameChild
            F("NameChild/valid", PlayerActionKind.NameChild,
                s => Payload(new { childNetworkId = s.Get(s.Child), name = "Ada" }),
                null,
                () => { var s = new Scenario(); s.ShareHouseholdWithChild(); return s; }),
            F("NameChild/missing-child", PlayerActionKind.NameChild,
                _ => Payload(new { childNetworkId = 999_999UL, name = "Ada" }),
                "target_not_found"),
            F("NameChild/blank-name", PlayerActionKind.NameChild,
                s => Payload(new { childNetworkId = s.Get(s.Child), name = "  " }),
                "invalid_state",
                () => { var s = new Scenario(); s.ShareHouseholdWithChild(); return s; }),

            // ---- ChooseContinuation
            F("ChooseContinuation/valid", PlayerActionKind.ChooseContinuation,
                _ => Payload(new { policy = "Heir" }),
                null),
            F("ChooseContinuation/unknown-policy", PlayerActionKind.ChooseContinuation,
                _ => Payload(new { policy = "NotAPolicy" }),
                "invalid_state")
        };
    }

    /// <summary>One fixture world: a player, a partner, an office with a posting, a property, a store, a venue and a child.</summary>
    private sealed class Scenario : IDisposable
    {
        public readonly World World = Arch.Core.World.Create();
        public readonly EconomyContext Economy;
        public readonly NetworkIdRegistry NetworkIds = new();
        public readonly PlayerRegistry Players = new();
        public readonly RelationshipIndex Relationships = new();
        public readonly SocialGraphService SocialGraph = new();
        public readonly VenueOptions VenueOptions = new() { ActivityCooldownTicks = 5, RecentActivityTicks = 100 };
        public readonly RelationshipService RelationshipService;
        public readonly PregnancyIndex Pregnancies = new();
        public readonly PregnancyService PregnancyService;
        public readonly PlayerRelationshipService PlayerRelationships;
        public readonly VenueService Venues;
        public readonly VenueActivityService VenueActivities;
        public readonly PlayerActionService Actions;

        public readonly Entity Genos;
        public readonly Entity Partner;
        public readonly Entity Company;
        public readonly Entity Office;
        public readonly Entity Posting;
        public readonly Entity Property;
        public readonly Entity Unit;
        public readonly Entity UnitB;
        public readonly Entity Venue;
        public readonly Entity Child;

        private readonly Dictionary<Entity, ulong> ids = new();

        public Scenario()
        {
            Economy = new EconomyContext(World, new EmploymentIndex(), clock: SimulationClock.Default);
            Economy.Housing.AttachNetworkIdAllocator(entity => NetworkIds.Allocate(entity));

            RelationshipService = new RelationshipService(World, Relationships)
            {
                NetworkIdAllocator = entity => NetworkIds.Allocate(entity),
                NetworkIdRemover = entity => NetworkIds.Remove(entity)
            };
            Pregnancies.AttachWorld(World);
            PregnancyService = new PregnancyService(World, Pregnancies)
            {
                NetworkIdAllocator = entity => NetworkIds.Allocate(entity),
                NetworkIdRemover = entity => NetworkIds.Remove(entity)
            };

            Genos = World.Create(
                new GenName("Player One"),
                new Needs { Hunger = 10f, Energy = 50f, Hygiene = 50f, Bladder = 50f },
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(30),
                new Wallet(Money.FromUnits(5_000m)),
                new SkillProfile(),
                new Health(100f),
                new PlayerControlled("p1"),
                new Lifecycle(LifeStage.Work, 0, 0),
                new Traits { Sex = Sex.Male },
                new TilePosition(new TileCoord(11, 10)));
            Register(Genos, 100);

            Partner = World.Create(
                new GenName("Player Two"),
                new Needs(),
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(29),
                new Wallet(Money.FromUnits(1_000m)),
                new SkillProfile(),
                new Health(100f),
                new PlayerControlled("p2"),
                new Lifecycle(LifeStage.Work, 0, 0),
                new Traits { Sex = Sex.Female },
                new TilePosition(new TileCoord(12, 10)));
            Register(Partner, 700);
            Players.GetOrCreate("p1", Genos, 0);
            Players.GetOrCreate("p2", Partner, 0);

            Company = World.Create(
                new CompanyName("Acme"),
                CompanyAccount.Create(Money.FromUnits(100_000m), 0));
            Register(Company, 200);

            Office = World.Create(
                new Office("HQ", Company, capacity: 10),
                new Placement(new TileCoord(10, 10), new Footprint(2, 2), new TileCoord(11, 10), BuildingKind.Office));
            Register(Office, 300);

            Posting = World.Create(
                new JobPosting(Company, Office, "Clerk", Money.FromUnits(1_200m), Skill.None, openSeats: 1, totalSeats: 1));
            Register(Posting, 400);
            var seat = World.Create(new OpenPosition(Posting), new NetworkId(401));
            NetworkIds.AllocateWithValue(seat, new NetworkId(401));

            Property = World.Create(
                new Placement(new TileCoord(20, 20), new Footprint(2, 2), new TileCoord(21, 20), BuildingKind.Residential));
            Register(Property, 500);

            Unit = World.Create(new HousingUnit(Property, "A1", HousingTier.Basic, capacity: 2, Money.FromUnits(300m)));
            Register(Unit, 501);
            UnitB = World.Create(new HousingUnit(Property, "A2", HousingTier.Basic, capacity: 2, Money.FromUnits(300m)));
            Register(UnitB, 502);

            Venue = World.Create(new SocialVenue
            {
                Kind = SocialVenueKind.Cafe,
                Capacity = 10,
                OpenFromTick = 0,
                OpenToTick = 10_000,
                Activities = VenueActivityMask.Sit | VenueActivityMask.Talk | VenueActivityMask.Cheer
            });
            World.Add(Venue, new Placement(new TileCoord(30, 30), new Footprint(2, 2), new TileCoord(31, 30), BuildingKind.Civic));
            Register(Venue, 900);

            Child = World.Create(
                new GenName("Baby"),
                new Alive(1),
                new Lifecycle(LifeStage.Education, 0, 0),
                new Traits { Sex = Sex.Female },
                new Needs());
            Register(Child, 800);

            Venues = new VenueService(World, Players, Economy, VenueOptions);
            VenueActivities = new VenueActivityService(World, Economy, Players, Venues, SocialGraph, VenueOptions);
            PlayerRelationships = new PlayerRelationshipService(
                World,
                Economy,
                Players,
                Relationships,
                RelationshipService,
                SocialGraph,
                RelationshipOptions.Default);

            Actions = new PlayerActionService(
                World,
                Economy,
                NetworkIds,
                Players,
                Relationships,
                RelationshipService,
                Pregnancies,
                PregnancyService,
                RelationshipOptions.Default,
                ReproductionOptions.Default,
                PlayerRelationships,
                Venues,
                VenueActivities);
        }

        public void Dispose() => World.Dispose();

        public ulong Get(Entity entity) => ids.TryGetValue(entity, out var id) ? id : 0UL;

        private ulong Register(Entity entity, ulong id)
        {
            World.Add(entity, new NetworkId(id));
            NetworkIds.AllocateWithValue(entity, new NetworkId(id));
            ids[entity] = id;
            return id;
        }

        public void MoveFar() => World.Get<TilePosition>(Genos) = new TilePosition(new TileCoord(500, 500));

        public void StandAtProperty() => World.Get<TilePosition>(Genos) = new TilePosition(new TileCoord(21, 20));

        public void ZeroWallet() => World.Get<Wallet>(Genos) = new Wallet(Money.Zero);

        public void Employ() =>
            World.Add(Genos, new Employee
            {
                Company = Company,
                Office = Office,
                Position = "Clerk",
                SalaryPerCycle = Money.FromUnits(1_200m),
                HiredAtTick = 1
            });

        public void GiveJobOffer(long expiresTick) =>
            World.Add(Genos, new PlayerJobOffer
            {
                OfferId = Guid.NewGuid(),
                Posting = Posting,
                Office = Office,
                Position = "Clerk",
                SalaryPerCycle = Money.FromUnits(1_200m),
                CreatedTick = Tick,
                ExpiresTick = expiresTick
            });

        public void ListUnit() => World.Get<HousingUnit>(Unit) = World.Get<HousingUnit>(Unit) with { IsVacant = true };

        public void GiveLeaseOffer(long expiresTick) =>
            World.Add(Genos, new PlayerLeaseOffer
            {
                OfferId = Guid.NewGuid(),
                Unit = Unit,
                Property = Property,
                MonthlyRent = Money.FromUnits(300m),
                CreatedTick = Tick,
                ExpiresTick = expiresTick
            });

        /// <summary>Puts the player in a household that already occupies <paramref name="unit"/>.</summary>
        public void HouseGenosIn(Entity unit)
        {
            var household = World.Create(new Household { Head = Genos, MemberCount = 1 });
            World.Add(Genos, new HouseholdMember(household, HouseholdRole.Head));
            Economy.Housing.AddMember(Genos, household);
            ListUnit();
            World.Get<HousingUnit>(unit) = World.Get<HousingUnit>(unit) with { IsVacant = false };
            new HousingAllocator(World, Economy).TryAssign(household, unit, Tenure.Renter, Tick);
        }

        public void WithArrears()
        {
            var household = World.Create(new Household
            {
                Head = Genos,
                MemberCount = 1,
                RentArrears = Money.FromUnits(100m)
            });
            World.Add(Genos, new HouseholdMember(household, HouseholdRole.Head));
            Economy.Housing.AddMember(Genos, household);
        }

        public void FillPantry(int foodUnits) =>
            World.Add(Genos, new HouseholdPantry { FoodUnits = foodUnits });

        public void StockBread(int stock)
        {
            var index = GroceryCatalog.IndexOf("bread");
            var item = GroceryCatalog.Items[index];
            var inventory = World.Create(
                new StoreInventory
                {
                    ItemIndex = index,
                    Stock = stock,
                    CurrentPrice = GroceryPricingPolicy.Price(item, stock, stock, 0),
                    LastRestockTick = Tick
                },
                new StoreStockLink { Store = Office });
            Register(inventory, 950);
        }

        public void MakePartners() =>
            RelationshipService.TryCreatePair(
                Genos,
                Partner,
                RelationshipKind.Partner,
                RelationshipSource.Player,
                Tick,
                out _,
                out _,
                out _);

        public void EnterVenue()
        {
            World.Get<TilePosition>(Genos) = new TilePosition(new TileCoord(31, 30));
            Venues.TryEnter("p1", Venue, Tick, out _, out _);
        }

        public void ActivityOnce() =>
            VenueActivities.TryPerform("p1", Venue, VenueActivity.Talk, Tick);

        public void ReceiveInvitation(bool toPartner = false)
        {
            var invitation = World.Create(new FamilyInvitation
            {
                Id = Guid.Empty,
                From = toPartner ? Genos : Partner,
                To = toPartner ? Partner : Genos,
                Kind = FamilyInvitationKind.Partnership,
                Status = FamilyInvitationStatus.Pending,
                CreatedTick = Tick,
                ExpiresTick = Tick + 100
            });
            Register(invitation, 970);
        }

        public void ShareHouseholdWithChild()
        {
            var household = World.Create(new Household { Head = Genos, MemberCount = 2 });
            World.Add(Genos, new HouseholdMember(household, HouseholdRole.Head));
            World.Add(Child, new HouseholdMember(household, HouseholdRole.Child));
            Economy.Housing.AddMember(Genos, household);
            Economy.Housing.AddMember(Child, household);
        }
    }
}