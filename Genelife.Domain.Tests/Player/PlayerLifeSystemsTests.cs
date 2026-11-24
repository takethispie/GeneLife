using Arch.Core;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Player;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Messages.Events;
using Genelife.Player;
using Genelife.Systems;
using Genelife.Values;

namespace Genelife.UnitTests.Players;

public class PlayerLifeSystemsTests
{
    [Fact]
    public void NewSimContinuation_AttachesControlAfterDeath()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var players = new PlayerRegistry();
        var dead = world.Create(new GenName("Dead"), new Alive(70), new Dead());
        var profile = players.GetOrCreate("p1", dead, 0);
        profile.ContinuationPolicy = PlayerContinuationPolicy.NewSim;
        var created = world.Create(new GenName("Fresh"), new Alive(25), new Wallet(Money.Zero));
        var system = new PlayerLifecycleSystem(
            world,
            economy,
            players,
            newSimFactory: _ => created);

        system.Update(100);
        Assert.True(profile.AwaitingContinuation);
        Assert.Equal(Entity.Null, profile.ControlledSim);

        system.Update(200);
        Assert.False(profile.AwaitingContinuation);
        Assert.Equal(created, profile.ControlledSim);
        Assert.True(world.Has<PlayerControlled>(created));
        Assert.Equal("p1", world.Get<PlayerControlled>(created).PlayerId);
        Assert.True(players.TryGetBySim(created, out var bySim));
        Assert.Same(profile, bySim);
    }

    [Fact]
    public void RespawnPolicy_CapturesTheDeadSimAndWaitsForThePlayer()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var players = new PlayerRegistry();
        var traits = new Traits { Sex = Sex.Female, Aptitude = Aptitude.Art };
        var dead = world.Create(
            new GenName("Dead"),
            new Alive(70),
            new Dead(),
            traits);
        var profile = players.GetOrCreate("p1", dead, 0);
        var system = new PlayerLifecycleSystem(world, economy, players);

        system.Update(100);
        system.Update(200);

        Assert.True(profile.AwaitingContinuation);
        Assert.Equal(Entity.Null, profile.ControlledSim);
        Assert.Equal("Dead", profile.LastSimName);
        Assert.Equal(70, profile.LastSimAge);
        Assert.Equal(traits, profile.LastSimTraits!.Value);
        Assert.True(profile.LastSimDeathTick > 0);
        Assert.False(players.TryGetBySim(dead, out _));
    }

    [Fact]
    public void ObserverPolicy_DeathNoticeCannotRespawn()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var players = new PlayerRegistry();
        var dead = world.Create(new GenName("Observer Dead"), new Alive(64), new Dead());
        var profile = players.GetOrCreate("p1", dead, 0);
        profile.ContinuationPolicy = PlayerContinuationPolicy.Observer;
        var system = new PlayerLifecycleSystem(world, economy, players);

        system.Update(100);

        var notice = economy.DrainEvents().OfType<PlayerAwaitingContinuation>().Single();
        Assert.False(notice.CanRespawn);
    }

    [Fact]
    public void ObserverPolicy_StaysAwaitingWithoutCreatingASim()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var players = new PlayerRegistry();
        var dead = world.Create(new GenName("Dead"), new Alive(70), new Dead());
        var profile = players.GetOrCreate("p1", dead, 0);
        profile.ContinuationPolicy = PlayerContinuationPolicy.Observer;
        var system = new PlayerLifecycleSystem(world, economy, players);

        system.Update(100);
        system.Update(200);

        Assert.True(profile.AwaitingContinuation);
        Assert.Equal(Entity.Null, profile.ControlledSim);
    }

    [Fact]
    public void Employment_RecordsAttendanceAndTerminatesAfterThreeMissedMonths()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var company = world.Create(new CompanyName("Acme"), CompanyAccount.Create(Money.FromUnits(10_000m), 0));
        var office = world.Create(new Office("HQ", company));
        var sim = world.Create(
            new GenName("Player"),
            new Alive(30),
            new Employee(company, office, "Clerk", Money.FromUnits(1_000m)),
            new PlayerEmployment(office, 0, PlayerEmploymentSource.PlayerApplied)
            {
                AttendedThisMonth = true
            });
        var system = new PlayerEmploymentSystem(world, economy, interval: 1);

        system.Update(100);
        var record = world.Get<EmploymentRecord>(sim);
        Assert.Equal(1, record.ShiftsExpected);
        Assert.Equal(1, record.ShiftsCompleted);
        var player = world.Get<PlayerEmployment>(sim);
        Assert.False(player.AttendedThisMonth);
        Assert.Equal(0, player.ConsecutiveMissedShifts);

        system.Update(200);
        system.Update(300);
        Assert.True(world.Has<Employee>(sim));
        system.Update(400);

        Assert.False(world.Has<Employee>(sim));
        Assert.False(world.Has<PlayerEmployment>(sim));
    }
}
