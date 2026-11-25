using Arch.Core;
using Genelife.Api.Services;
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

namespace Genelife.UnitTests.Social;

public sealed class PlayerRelationshipTests : IDisposable
{
    private readonly World world = World.Create();
    private readonly EconomyContext economy;
    private readonly NetworkIdRegistry networkIds = new();
    private readonly PlayerRegistry players = new();
    private readonly RelationshipIndex relationships = new();
    private readonly RelationshipService relationshipService;
    private readonly SocialGraphService social = new();
    private readonly PlayerRelationshipService service;
    private readonly Entity alice;
    private readonly Entity bob;

    public PlayerRelationshipTests()
    {
        economy = new EconomyContext(world, clock: SimulationClock.Default);
        economy.Housing.AttachNetworkIdAllocator(entity => networkIds.Allocate(entity));
        relationshipService = new RelationshipService(world, relationships)
        {
            NetworkIdAllocator = entity => networkIds.Allocate(entity),
            NetworkIdRemover = entity => networkIds.Remove(entity)
        };

        alice = CreatePlayer("alice", "Alice", 100, Sex.Female);
        bob = CreatePlayer("bob", "Bob", 200, Sex.Male);
        service = new PlayerRelationshipService(
            world,
            economy,
            players,
            relationships,
            relationshipService,
            social,
            RelationshipOptions.Default);
    }

    [Fact]
    public void AcceptingAPartnershipInvitationCreatesMirroredEdgesWithoutMovingHouseholds()
    {
        CreateHousehold(alice, capacity: 2);
        CreateHousehold(bob, capacity: 2);

        var invite = service.InvitePartner("alice", "bob", tick: 10);
        Assert.True(invite.Applied, invite.Code);
        var pending = Assert.Single(service.PendingInvitations("bob", 10));

        var accepted = service.Respond("bob", pending.Id, accept: true, tick: 11);

        Assert.True(accepted.Applied, accepted.Code);
        Assert.Contains(bob, relationships.PartnersOf(alice));
        Assert.Contains(alice, relationships.PartnersOf(bob));
        Assert.NotEqual(economy.Housing.HouseholdOf(alice), economy.Housing.HouseholdOf(bob));
        Assert.Equal("alice", service.BuildStatus("bob", 11).PartnerPlayerId);
        Assert.Contains(service.BuildStatus("alice", 11).Invitations, invitation => invitation.Status == FamilyInvitationStatus.Accepted);
    }

    [Fact]
    public void DecliningStartsACooldownAndExpiryCreatesNoEdge()
    {
        var invite = service.InvitePartner("alice", "bob", tick: 10);
        var declined = service.Respond("bob", invite.Invitation!.Id, accept: false, tick: 11);

        Assert.True(declined.Applied);
        Assert.Equal("cooldown", service.InvitePartner("alice", "bob", tick: 12).Code);
        Assert.True(service.InvitePartner("alice", "bob", tick: 611).Applied);
        Assert.Empty(relationships.PartnersOf(alice));

        var second = service.PendingInvitations("bob", 612).Single();
        var expired = service.Respond("bob", second.Id, accept: true, tick: 712);

        Assert.False(expired.Applied);
        Assert.Equal("offer_expired", expired.Code);
        Assert.Empty(relationships.PartnersOf(alice));
    }

    [Fact]
    public void BlocksCancelPendingInvitationsAndDenyNewOnes()
    {
        var invite = service.InvitePartner("alice", "bob", tick: 10);
        Assert.True(invite.Applied);

        Assert.True(social.Block("bob", "alice", tick: 11).Accepted);

        var cancelled = service.FindInvitation(invite.Invitation!.Id, 12);
        Assert.NotNull(cancelled);
        Assert.Equal(FamilyInvitationStatus.Cancelled, cancelled!.Status);
        Assert.Equal("blocked", service.InvitePartner("alice", "bob", tick: 13).Code);
        Assert.False(service.Respond("bob", invite.Invitation.Id, accept: true, tick: 14).Applied);
    }

    [Fact]
    public void CohabitationRequiresASeparateAcceptanceAndRespectsCapacity()
    {
        CreateHousehold(alice, capacity: 1);
        CreateHousehold(bob, capacity: 1);

        var partnership = service.InvitePartner("alice", "bob", tick: 10);
        Assert.True(service.Respond("bob", partnership.Invitation!.Id, accept: true, tick: 11).Applied);
        Assert.NotEqual(economy.Housing.HouseholdOf(alice), economy.Housing.HouseholdOf(bob));

        var cohabit = service.InviteCohabit("alice", "bob", tick: 12);
        Assert.True(cohabit.Applied, cohabit.Code);

        var rejected = service.Respond("bob", cohabit.Invitation!.Id, accept: true, tick: 13);
        Assert.False(rejected.Applied);
        Assert.Equal("merge_unavailable", rejected.Code);
        Assert.NotEqual(economy.Housing.HouseholdOf(alice), economy.Housing.HouseholdOf(bob));

        Assert.True(service.Respond("bob", cohabit.Invitation.Id, accept: false, tick: 14).Applied);
    }

    [Fact]
    public void CohabitationMergeSucceedsWithCapacityAndEndPartnershipSplitsHousehold()
    {
        CreateHousehold(alice, capacity: 3);
        CreateHousehold(bob, capacity: 3);

        var partnership = service.InvitePartner("alice", "bob", tick: 10);
        service.Respond("bob", partnership.Invitation!.Id, accept: true, tick: 11);

        var cohabit = service.InviteCohabit("alice", "bob", tick: 12);
        var accepted = service.Respond("bob", cohabit.Invitation!.Id, accept: true, tick: 13);

        Assert.True(accepted.Applied, accepted.Code);
        Assert.Equal(economy.Housing.HouseholdOf(alice), economy.Housing.HouseholdOf(bob));
        Assert.True(service.BuildStatus("alice", 13).SharesHousehold);

        var ended = service.EndPartnership("alice", "bob", tick: 14);

        Assert.True(ended.Applied, ended.Code);
        Assert.Contains(bob, relationships.ExPartnersOf(alice));
        Assert.Contains(alice, relationships.ExPartnersOf(bob));
        Assert.Empty(relationships.PartnersOf(alice));
        Assert.Empty(relationships.PartnersOf(bob));
        Assert.NotEqual(economy.Housing.HouseholdOf(alice), economy.Housing.HouseholdOf(bob));
    }

    [Fact]
    public void InvitationsSurviveContinuationAndResolveTheCurrentGenos()
    {
        var invite = service.InvitePartner("alice", "bob", tick: 10);
        Assert.True(invite.Applied, invite.Code);

        var heir = CreateGenos("Alice Heir", 300, Sex.Female);
        Assert.True(players.TryGet("alice", out var profile));
        players.AttachGenos(profile, heir);

        var accepted = service.Respond("bob", invite.Invitation!.Id, accept: true, tick: 11);

        Assert.True(accepted.Applied, accepted.Code);
        Assert.Contains(bob, relationships.PartnersOf(heir));
        Assert.Contains(heir, relationships.PartnersOf(bob));
        Assert.Empty(relationships.PartnersOf(alice));
    }

    [Fact]
    public void EndingAPartnershipDoesNotEndFriendship()
    {
        social.AddFriend("alice", "bob", tick: 1);
        var partnership = service.InvitePartner("alice", "bob", tick: 10);
        service.Respond("bob", partnership.Invitation!.Id, accept: true, tick: 11);

        Assert.True(service.EndPartnership("alice", "bob", tick: 12, splitHousehold: false).Applied);

        Assert.True(social.AreFriends("alice", "bob"));
        Assert.False(social.IsBlocked("alice", "bob"));
    }

    private Entity CreatePlayer(string playerId, string name, ulong networkId, Sex sex)
    {
        var genos = CreateGenos(name, networkId, sex);
        players.GetOrCreate(playerId, genos, 0);
        return genos;
    }

    private Entity CreateGenos(string name, ulong networkId, Sex sex)
    {
        var genos = world.Create(
            new GenName(name),
            new Needs(),
            new Alive(30),
            new Wallet(Money.FromUnits(1_000m)),
            new SkillProfile(),
            new Health(100f),
            new Lifecycle(LifeStage.Work, 0, 0),
            new Traits { Sex = sex });
        world.Add(genos, new NetworkId(networkId));
        networkIds.AllocateWithValue(genos, new NetworkId(networkId));
        return genos;
    }

    private Entity CreateHousehold(Entity head, int capacity)
    {
        var property = world.Create(new Property("Test Property", "Central", Entity.Null, 1));
        world.Add(property, networkIds.Allocate(property));
        economy.Housing.AddProperty(property, Entity.Null);
        var unit = world.Create(new HousingUnit(
            property,
            "1",
            HousingTier.Basic,
            capacity,
            HousingTierPolicy.BaseRent(HousingTier.Basic),
            1f));
        world.Add(unit, networkIds.Allocate(unit));
        economy.Housing.AddUnit(unit, property);

        var household = world.Create(new Household
        {
            Head = head,
            Tenure = Tenure.Renter,
            MonthlyRent = Money.FromUnits(100m)
        });
        world.Add(household, networkIds.Allocate(household));
        world.Add(head, new HouseholdMember(household, HouseholdRole.Head));
        economy.Housing.AddMember(head, household);
        economy.Housing.MoveIn(unit, household);
        return household;
    }

    public void Dispose() => world.Dispose();
}
