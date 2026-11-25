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
    public void NewGenosContinuation_AttachesControlAfterDeath()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var players = new PlayerRegistry();
        var dead = world.Create(new GenName("Dead"), new Alive(70), new Dead());
        var profile = players.GetOrCreate("p1", dead, 0);
        profile.ContinuationPolicy = PlayerContinuationPolicy.NewGenos;
        var created = world.Create(new GenName("Fresh"), new Alive(25), new Wallet(Money.Zero));
        var system = new PlayerLifecycleSystem(
            world,
            economy,
            players,
            newGenosFactory: _ => created);

        system.Update(100);
        Assert.True(profile.AwaitingContinuation);
        Assert.Equal(Entity.Null, profile.ControlledGenos);

        system.Update(200);
        Assert.False(profile.AwaitingContinuation);
        Assert.Equal(created, profile.ControlledGenos);
        Assert.True(world.Has<PlayerControlled>(created));
        Assert.Equal("p1", world.Get<PlayerControlled>(created).PlayerId);
        Assert.True(players.TryGetByGenos(created, out var byGenos));
        Assert.Same(profile, byGenos);
    }

    [Fact]
    public void RespawnPolicy_CapturesTheDeadGenosAndWaitsForThePlayer()
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
        Assert.Equal(Entity.Null, profile.ControlledGenos);
        Assert.Equal("Dead", profile.LastGenosName);
        Assert.Equal(70, profile.LastGenosAge);
        Assert.Equal(traits, profile.LastGenosTraits!.Value);
        Assert.True(profile.LastGenosDeathTick > 0);
        Assert.False(players.TryGetByGenos(dead, out _));
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
    public void ObserverPolicy_StaysAwaitingWithoutCreatingAGenos()
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
        Assert.Equal(Entity.Null, profile.ControlledGenos);
    }

    [Fact]
    public void Employment_RecordsAttendanceAndTerminatesAfterThreeMissedMonths()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var company = world.Create(new CompanyName("Acme"), CompanyAccount.Create(Money.FromUnits(10_000m), 0));
        var office = world.Create(new Office("HQ", company));
        var genos = world.Create(
            new GenName("Player"),
            new Alive(30),
            new Employee(company, office, "Clerk", Money.FromUnits(1_000m)),
            new PlayerEmployment(office, 0, PlayerEmploymentSource.PlayerApplied)
            {
                AttendedThisMonth = true
            });
        var system = new PlayerEmploymentSystem(world, economy, interval: 1);

        system.Update(100);
        var record = world.Get<EmploymentRecord>(genos);
        Assert.Equal(1, record.ShiftsExpected);
        Assert.Equal(1, record.ShiftsCompleted);
        var player = world.Get<PlayerEmployment>(genos);
        Assert.False(player.AttendedThisMonth);
        Assert.Equal(0, player.ConsecutiveMissedShifts);

        system.Update(200);
        system.Update(300);
        Assert.True(world.Has<Employee>(genos));
        system.Update(400);

        Assert.False(world.Has<Employee>(genos));
        Assert.False(world.Has<PlayerEmployment>(genos));
    }
}
