using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Values;

namespace Genelife.UnitTests.Family;

public class PregnancyProgressionTests
{
    [Fact]
    public void PrenatalCareIsChargedAndTransferred()
    {
        var context = new Context(wallet: Money.FromUnits(1_000m));
        var pregnancy = context.Start(dueTick: 1_000, tick: 100);
        var before = context.Economy.TotalMoneySupply();

        context.Run(tick: 200);

        var data = context.World.Get<Pregnancy>(pregnancy);
        Assert.Equal(Money.FromUnits(40m), data.CarePaid);
        Assert.Equal(Money.Zero, data.CareArrears);
        Assert.Equal(Money.FromUnits(40m), context.Economy.Government.Balance);
        Assert.Equal(before, context.Economy.TotalMoneySupply());
        Assert.Equal(PregnancyStatus.Active, data.Status);
    }

    [Fact]
    public void NoFundsProducesArrearsAndHigherRiskWithoutNegativeMoney()
    {
        var context = new Context(wallet: Money.Zero);
        var pregnancy = context.Start(dueTick: 1_000, tick: 100);

        context.Run(tick: 200);

        var data = context.World.Get<Pregnancy>(pregnancy);
        Assert.Equal(Money.Zero, data.CarePaid);
        Assert.Equal(Money.FromUnits(40m), data.CareArrears);
        Assert.True(data.RiskBasisPoints > 0);
        Assert.False(context.World.Get<Wallet>(context.Other).Balance.IsNegative);
        Assert.Equal(Money.Zero, context.Economy.Government.Balance);
    }

    [Fact]
    public void DuePregnancyBecomesReadyForBirthWithoutAnotherCareCharge()
    {
        var context = new Context(wallet: Money.FromUnits(1_000m));
        var pregnancy = context.Start(dueTick: 300, tick: 100);

        context.Run(tick: 300);

        Assert.Equal(PregnancyStatus.ReadyToBirth, context.World.Get<Pregnancy>(pregnancy).Status);
        Assert.Equal(Money.Zero, context.World.Get<Pregnancy>(pregnancy).CarePaid);
    }

    [Fact]
    public void LossSetsStatusAndStartsCooldown()
    {
        var context = new Context(wallet: Money.FromUnits(1_000m));
        var options = new ReproductionOptions { MonthlyLossChance = 1m, MonthlyPrenatalCareCost = Money.Zero };
        var pregnancy = context.Start(dueTick: 1_000, tick: 100);

        context.Run(tick: 200, options);

        Assert.Equal(PregnancyStatus.Lost, context.World.Get<Pregnancy>(pregnancy).Status);
        Assert.True(context.Pregnancies.IsInLossCooldown(
            context.Carrying, 300, options, SimulationClock.Default));
        Assert.Contains(context.Economy.PendingEvents,
            @event => @event is Genelife.Messages.Events.PregnancyLost);
    }

    private sealed class Context
    {
        public Context(Money wallet)
        {
            Economy = new EconomyContext(World);
            Pregnancies = new PregnancyIndex();
            Service = new PregnancyService(World, Pregnancies);
            Carrying = World.Create(
                new GenName("Carrying"),
                new Alive(30),
                new Health(80f),
                new Traits { Sex = Sex.Female },
                new NetworkId(1));
            Other = World.Create(
                new GenName("Other"),
                new Alive(31),
                new Health(90f),
                new Traits { Sex = Sex.Male },
                new NetworkId(2),
                new Wallet(wallet));
            Household = World.Create(new Household { Head = Other, Tenure = Tenure.Renter });
            World.Add(Other, new HouseholdMember(Household, HouseholdRole.Head));
            World.Add(Carrying, new HouseholdMember(Household, HouseholdRole.Partner));
            Economy.Housing.AddMember(Other, Household);
            Economy.Housing.AddMember(Carrying, Household);
        }

        public World World { get; } = World.Create();
        public EconomyContext Economy { get; }
        public PregnancyIndex Pregnancies { get; }
        public PregnancyService Service { get; }
        public Entity Carrying { get; }
        public Entity Other { get; }
        public Entity Household { get; }

        public Entity Start(long dueTick, long tick)
        {
            Assert.True(Service.TryStart(
                Carrying, Other, tick, dueTick, 80, PregnancySource.Conception,
                new ReproductionOptions(), out var pregnancy, out _));
            return pregnancy;
        }

        public void Run(long tick, ReproductionOptions? options = null)
        {
            options ??= new ReproductionOptions();
            new PregnancySystem(World, Economy, Pregnancies, Service, options).Update(tick);
        }
    }
}
