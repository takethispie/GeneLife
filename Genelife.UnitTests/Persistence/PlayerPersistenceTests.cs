using Arch.Core;
using Genelife.Api.Commands;
using Genelife.Api.Persistence;
using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Player;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Player;
using Genelife.Values;

namespace Genelife.UnitTests.Persistence;

public class PlayerPersistenceTests
{
    [Fact]
    public void PlayerProfileJobLeasePantryOffersAndInvitationsRoundTrip()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();

        var playerId = "player-persist";
        var simNetworkId = engine.EnsurePlayerSim(playerId);
        Assert.NotEqual(0UL, simNetworkId);

        ulong officeNetworkId = 0;
        ulong postingNetworkId = 0;
        ulong householdNetworkId = 0;
        Guid invitationId = Guid.NewGuid();
        engine.ExecuteWithWorld(world =>
        {
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(simNetworkId), out var sim));
            engine.Players.TryGet(playerId, out var profile);
            profile.ContinuationPolicy = PlayerContinuationPolicy.NewSim;

            ulong Register(Entity entity)
            {
                var id = engine.NetworkIds.Allocate(entity);
                world.Add(entity, id);
                return id.Value;
            }

            var company = world.Create(
                new CompanyName("Persist Co"),
                CompanyAccount.Create(Money.FromUnits(1_000m), 0));
            Register(company);

            var office = world.Create(new Office("Persist HQ", company));
            Register(office);
            var posting = world.Create(new JobPosting(company, office, "Clerk", Money.FromUnits(900m)));
            Register(posting);
            officeNetworkId = engine.NetworkIds.TryGetNetworkId(office, out var officeId) ? officeId.Value : 0;
            postingNetworkId = engine.NetworkIds.TryGetNetworkId(posting, out var postingId) ? postingId.Value : 0;

            world.Add(sim, new PlayerEmployment(office, 5, PlayerEmploymentSource.PlayerAcceptedOffer)
            {
                LastAttendedTick = 17,
                ConsecutiveMissedShifts = 1,
                AttendedThisMonth = true
            });
            world.Add(sim, new PlayerJobOffer
            {
                OfferId = Guid.NewGuid(),
                Posting = posting,
                Office = office,
                Position = "Clerk",
                SalaryPerCycle = Money.FromUnits(900m),
                CreatedTick = 10,
                ExpiresTick = 90
            });

            var property = world.Create(new Property("Persist Homes", "Central", Entity.Null, 1));
            Register(property);
            engine.Economy!.Housing.AddProperty(property, Entity.Null);
            var unit = world.Create(new HousingUnit(
                property,
                "2B",
                HousingTier.Basic,
                HousingTierPolicy.Capacity(HousingTier.Basic),
                HousingTierPolicy.BaseRent(HousingTier.Basic),
                1f));
            Register(unit);
            engine.Economy.Housing.AddUnit(unit, property);

            var households = new HouseholdRepository(world, engine.NetworkIds, engine.Economy);
            var householdId = households.Create(simNetworkId, 20);
            Assert.True(households.TryGet(householdId, out var household));
            householdNetworkId = householdId.Value;
            Assert.True(new HousingAllocator(world, engine.Economy).TryAssign(
                household, unit, Tenure.Renter, 20, rentOverride: Money.FromUnits(140m)));
            world.Add(sim, new PlayerHousing(household, unit, 20, Money.Zero));
            world.Add(household, new HouseholdPantry
            {
                FoodUnits = 3,
                HouseholdGoods = 2,
                LastPurchaseTick = 22
            });

            world.Add(sim, new PlayerLeaseOffer
            {
                OfferId = Guid.NewGuid(),
                Unit = unit,
                Property = property,
                MonthlyRent = Money.FromUnits(140m),
                CreatedTick = 21,
                ExpiresTick = 121
            });

            var partner = world.Create(new GenName("Partner"), new Needs(), new Alive(30));
            Register(partner);
            var invitation = world.Create(new FamilyInvitation(
                invitationId,
                sim,
                partner,
                FamilyInvitationKind.Partnership,
                30,
                130));
            Register(invitation);
            return 0;
        });

        var state = engine.CaptureState(engine.NetworkIds);
        var savedProfile = Assert.Single(state.PlayerProfiles.Where(p => p.PlayerId == playerId));
        Assert.Equal("NewSim", savedProfile.ContinuationPolicy);
        var savedLife = Assert.Single(state.PlayerLives.Where(l => l.SimNetworkId == simNetworkId));
        Assert.True(savedLife.HasEmployment);
        Assert.Equal(officeNetworkId, savedLife.EmploymentOfficeNetworkId);
        Assert.Equal(householdNetworkId, savedLife.HouseholdNetworkId);
        Assert.Equal(3, savedLife.FoodUnits);
        Assert.Equal(2, savedLife.HouseholdGoods);
        Assert.NotEqual(Guid.Empty, savedLife.JobOfferId);
        Assert.NotEqual(Guid.Empty, savedLife.LeaseOfferId);
        Assert.Contains(state.FamilyInvitations, invite => invite.Id == invitationId);
        var offerId = savedLife.JobOfferId;
        var leaseOfferId = savedLife.LeaseOfferId;

        engine.Shutdown();

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        Assert.True(restored.Players.TryGet(playerId, out var restoredProfile));
        Assert.Equal(PlayerContinuationPolicy.NewSim, restoredProfile.ContinuationPolicy);
        Assert.True(restored.NetworkIds.TryGetEntity(new NetworkId(simNetworkId), out var restoredSim));
        Assert.True(restored.World!.Has<PlayerEmployment>(restoredSim));
        Assert.Equal(17, restored.World.Get<PlayerEmployment>(restoredSim).LastAttendedTick);
        Assert.True(restored.World!.Has<PlayerJobOffer>(restoredSim));
        Assert.Equal(offerId, restored.World.Get<PlayerJobOffer>(restoredSim).OfferId);
        Assert.True(restored.World.Has<PlayerLeaseOffer>(restoredSim));
        Assert.Equal(leaseOfferId, restored.World.Get<PlayerLeaseOffer>(restoredSim).OfferId);
        Assert.True(restored.World.Has<PlayerHousing>(restoredSim));

        Assert.True(restored.NetworkIds.TryGetEntity(new NetworkId(householdNetworkId), out var restoredHousehold));
        Assert.True(restored.World.Has<HouseholdPantry>(restoredHousehold));
        Assert.Equal(3, restored.World.Get<HouseholdPantry>(restoredHousehold).FoodUnits);

        var invitations = new List<FamilyInvitation>();
        restored.World.Query(in new QueryDescription().WithAll<FamilyInvitation>(),
            (ref FamilyInvitation invitation) => invitations.Add(invitation));
        Assert.Contains(invitations, invitation => invitation.Id == invitationId);

        var second = restored.CaptureState(restored.NetworkIds);
        restored.Shutdown();

        Assert.Equal(state.PlayerProfiles.Count, second.PlayerProfiles.Count);
        Assert.Equal(state.PlayerLives.Count, second.PlayerLives.Count);
        Assert.Equal(state.FamilyInvitations.Count, second.FamilyInvitations.Count);
        var secondLife = second.PlayerLives.Single(l => l.SimNetworkId == simNetworkId);
        Assert.Equal(offerId, secondLife.JobOfferId);
        Assert.Equal(leaseOfferId, secondLife.LeaseOfferId);
    }

    [Fact]
    public void SchemaElevenSaveGainsPlayerProfilesFromControlledSims()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var simNetworkId = engine.EnsurePlayerSim("migrating-player");
        var state = engine.CaptureState(engine.NetworkIds);
        engine.Shutdown();

        state.SchemaVersion = 11;
        state.PlayerProfiles.Clear();
        state.PlayerLives.Clear();
        state.FamilyInvitations.Clear();
        SnapshotMigrations.Upgrade(state, 11);

        var profile = Assert.Single(state.PlayerProfiles);
        Assert.Equal("migrating-player", profile.PlayerId);
        Assert.Equal(simNetworkId, profile.SimNetworkId);
        Assert.Contains(state.PlayerLives, life => life.SimNetworkId == simNetworkId);
    }
}
