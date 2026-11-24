using System.Text.Json;
using Arch.Core;
using Genelife.Api.Commands;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Components.Player;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Housing;
using Genelife.Player;
using Genelife.Social;
using Genelife.Values;

namespace Genelife.UnitTests.Players;

public sealed class PlayerActionServiceTests : IDisposable
{
    private readonly World world = World.Create();
    private readonly EconomyContext economy;
    private readonly NetworkIdRegistry networkIds = new();
    private readonly PlayerRegistry players = new();
    private readonly RelationshipIndex relationships = new();
    private readonly RelationshipService relationshipService;
    private readonly PregnancyIndex pregnancies = new();
    private readonly PregnancyService pregnancyService;
    private readonly PlayerActionService actions;
    private readonly SocialGraphService socialGraph = new();
    private readonly PlayerRelationshipService playerRelationships;

    private readonly Entity sim;
    private readonly Entity company;
    private readonly Entity office;
    private readonly Entity posting;
    private readonly Entity property;
    private readonly Entity unit;

    public PlayerActionServiceTests()
    {
        economy = new EconomyContext(world, new EmploymentIndex(), clock: SimulationClock.Default);
        economy.Housing.AttachNetworkIdAllocator(entity => networkIds.Allocate(entity));

        relationshipService = new RelationshipService(world, relationships)
        {
            NetworkIdAllocator = entity => networkIds.Allocate(entity),
            NetworkIdRemover = entity => networkIds.Remove(entity)
        };
        pregnancies.AttachWorld(world);
        pregnancyService = new PregnancyService(world, pregnancies)
        {
            NetworkIdAllocator = entity => networkIds.Allocate(entity),
            NetworkIdRemover = entity => networkIds.Remove(entity)
        };

        sim = world.Create(
            new GenName("Player One"),
            new Needs { Hunger = 10f, Energy = 50f, Hygiene = 50f, Bladder = 50f },
            new CurrentAction(ActionType.Idle, 0f),
            new Alive(30),
            new Wallet(Money.FromUnits(5_000m)),
            new SkillProfile(),
            new Health(100f),
            new PlayerControlled("p1"),
            new Lifecycle(LifeStage.Work, 0, 0),
            new Traits { Sex = Sex.Male });
        Register(sim, 100);

        company = world.Create(
            new CompanyName("Acme"),
            CompanyAccount.Create(Money.FromUnits(100_000m), 0));
        Register(company, 200);

        office = world.Create(new Office("HQ", company, capacity: 10));
        Register(office, 300);

        posting = world.Create(new JobPosting(
            company,
            office,
            "Clerk",
            Money.FromUnits(1_200m),
            openSeats: 1,
            totalSeats: 1));
        Register(posting, 400);
        world.Create(new OpenPosition(posting));

        property = world.Create(new Property("Test Apartments", "Central", Entity.Null, 1));
        Register(property, 500);
        economy.Housing.AddProperty(property, Entity.Null);
        unit = world.Create(new HousingUnit(
            property,
            "1A",
            HousingTier.Basic,
            HousingTierPolicy.Capacity(HousingTier.Basic),
            HousingTierPolicy.BaseRent(HousingTier.Basic),
            1f));
        Register(unit, 501);
        economy.Housing.AddUnit(unit, property);

        players.GetOrCreate("p1", sim, 0);
        playerRelationships = new PlayerRelationshipService(
            world,
            economy,
            players,
            relationships,
            relationshipService,
            socialGraph,
            RelationshipOptions.Default);
        actions = new PlayerActionService(
            world,
            economy,
            networkIds,
            players,
            relationships,
            relationshipService,
            pregnancies,
            pregnancyService,
            RelationshipOptions.Default,
            ReproductionOptions.Default,
            playerRelationships);
    }

    private void Register(Entity entity, ulong id)
    {
        world.Add(entity, new NetworkId(id));
        networkIds.AllocateWithValue(entity, new NetworkId(id));
    }

    private static JsonElement Payload(object value) => JsonSerializer.SerializeToElement(value);

    [Fact]
    public void ApplyForJob_CreatesOffer_ThenAcceptHiresThroughNormalPath()
    {
        var applied = actions.Apply("p1", sim, PlayerActionKind.ApplyForJob,
            Payload(new { postingNetworkId = 400UL, officeNetworkId = 300UL }), tick: 10);

        Assert.True(applied.Applied, applied.Code);
        Assert.True(world.Has<PlayerJobOffer>(sim));
        var offer = world.Get<PlayerJobOffer>(sim);

        var accepted = actions.Apply("p1", sim, PlayerActionKind.AcceptJobOffer,
            Payload(new { offerId = offer.OfferId }), tick: 11);

        Assert.True(accepted.Applied, accepted.Code);
        Assert.True(world.Has<Employee>(sim));
        Assert.True(world.Has<PlayerEmployment>(sim));
        Assert.False(world.Has<PlayerJobOffer>(sim));
        Assert.Equal(Money.FromUnits(1_200m), world.Get<Employee>(sim).SalaryPerCycle);
        Assert.True(world.Get<JobPosting>(posting).IsClosed);
    }

    [Fact]
    public void BuyThenConsume_MovesMoneyAndFood_NoMoneyCreation()
    {
        var walletBefore = world.Get<Wallet>(sim).Balance;
        var supplyBefore = economy.TotalMoneySupply();

        var bought = actions.Apply("p1", sim, PlayerActionKind.BuyItem,
            Payload(new { storeNetworkId = 300UL, catalogItemId = "food", quantity = 2 }), tick: 12);

        Assert.True(bought.Applied, bought.Code);
        Assert.Equal(walletBefore - Money.FromUnits(50m), world.Get<Wallet>(sim).Balance);
        Assert.Equal(supplyBefore, economy.TotalMoneySupply());
        Assert.Equal(2, world.Get<HouseholdPantry>(sim).FoodUnits);

        var hungerBefore = world.Get<Needs>(sim).Hunger;
        var consumed = actions.Apply("p1", sim, PlayerActionKind.ConsumeItem,
            Payload(new { catalogItemId = "food", quantity = 1 }), tick: 13);

        Assert.True(consumed.Applied, consumed.Code);
        Assert.True(world.Get<Needs>(sim).Hunger > hungerBefore);
        Assert.Equal(1, world.Get<HouseholdPantry>(sim).FoodUnits);
    }

    [Fact]
    public void RequestThenAcceptLease_CreatesHouseholdAndLease()
    {
        var requested = actions.Apply("p1", sim, PlayerActionKind.RequestHousing,
            Payload(new { unitNetworkId = 501UL, propertyNetworkId = 0UL }), tick: 20);

        Assert.True(requested.Applied, requested.Code);
        var offer = world.Get<PlayerLeaseOffer>(sim);

        var accepted = actions.Apply("p1", sim, PlayerActionKind.AcceptLease,
            Payload(new { offerId = offer.OfferId }), tick: 21);

        Assert.True(accepted.Applied, accepted.Code);
        Assert.True(world.Has<PlayerHousing>(sim));
        var lease = world.Get<PlayerHousing>(sim);
        Assert.Equal(unit, lease.Unit);
        Assert.NotEqual(Entity.Null, lease.Household);
        Assert.Equal(unit, economy.Housing.UnitOf(lease.Household));
        Assert.True(world.Has<HouseholdPantry>(lease.Household));
    }

    [Fact]
    public void InvitePartner_ToAnotherPlayer_RequiresConsent_AndCreatesEdgeOnAccept()
    {
        var partner = world.Create(
            new GenName("Player Two"),
            new Needs(),
            new Alive(30),
            new Wallet(Money.FromUnits(1_000m)),
            new SkillProfile(),
            new Health(100f),
            new PlayerControlled("p2"),
            new Lifecycle(LifeStage.Work, 0, 0),
            new Traits { Sex = Sex.Female });
        Register(partner, 700);
        players.GetOrCreate("p2", partner, 0);

        var invited = actions.Apply("p1", sim, PlayerActionKind.InvitePartner,
            Payload(new { targetSimNetworkId = 700UL }), tick: 30);

        Assert.True(invited.Applied, invited.Code);
        var invitation = Assert.Single(playerRelationships.PendingInvitations("p2", 30));

        var responded = actions.Apply("p2", partner, PlayerActionKind.RespondToInvitation,
            Payload(new { invitationId = invitation.Id, accept = true }), tick: 31);

        Assert.True(responded.Applied, responded.Code);
        Assert.Contains(partner, relationships.PartnersOf(sim));
        Assert.Contains(sim, relationships.PartnersOf(partner));

        var ended = actions.Apply("p1", sim, PlayerActionKind.EndPartnership,
            Payload(new { partnerPlayerId = "p2", splitHousehold = false }), tick: 32);

        Assert.True(ended.Applied, ended.Code);
        Assert.Contains(partner, relationships.ExPartnersOf(sim));
        Assert.Contains(sim, relationships.ExPartnersOf(partner));
        Assert.Empty(relationships.PartnersOf(sim));
    }

    [Fact]
    public void ActionFromAnotherPlayer_IsRejectedAsNotOwner()
    {
        var other = world.Create(new GenName("Other"), new PlayerControlled("p2"), new Alive(30));
        Register(other, 800);

        var result = actions.Apply("p2", sim, PlayerActionKind.BuyItem,
            Payload(new { storeNetworkId = 300UL, catalogItemId = "food", quantity = 1 }), tick: 40);

        Assert.False(result.Applied);
        Assert.Equal("not_owner", result.Code);
    }

    private List<FamilyInvitation> PendingInvitations(Entity target)
    {
        var found = new List<FamilyInvitation>();
        world.Query(in new QueryDescription().WithAll<FamilyInvitation>(),
            (ref FamilyInvitation invitation) =>
            {
                if (invitation.To == target && invitation.Status == FamilyInvitationStatus.Pending)
                    found.Add(invitation);
            });
        return found;
    }

    public void Dispose() => world.Dispose();
}
