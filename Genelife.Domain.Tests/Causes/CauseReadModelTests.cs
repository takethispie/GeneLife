using System.Reflection;
using Arch.Core;
using Genelife.Api.DTOs;
using Genelife.Application.Causes;
using Genelife.Components.Consumption;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.UnitTests.Causes;

public class CauseReadModelTests
{
    [Fact]
    public void Build_emits_meal_and_money_facts_without_prose()
    {
        var world = World.Create();
        var entity = world.Create(
            new NetworkId(42UL),
            new GenName("Ada"),
            new Needs { Hunger = 61.24f, Energy = 80f, Hygiene = 90f, Bladder = 70f },
            new NutritionState { Balance = 12, ConsecutivePoorDays = 2, LastMealTick = 1_000 },
            new Wallet(Money.FromCents(1_234)),
            new SavingsAccount(Money.FromCents(500)) { LastInterestTick = 900 });

        var ledger = new FinanceLedger();
        ledger.Record(Guid.NewGuid(), 1_050, LedgerKind.ConsumerSpending, 42UL, null, Money.FromCents(320), "groceries");

        var facts = CauseReadModel.Build(world, entity, 1_220, ledger);

        var lastMeal = facts.Single(f => f.Code == CauseCodes.LastMeal);
        Assert.Equal(220, lastMeal.Value);
        Assert.Equal("ticks", lastMeal.Unit);
        Assert.Equal(1_000, lastMeal.SinceTick);

        var hunger = facts.Single(f => f.Code == CauseCodes.NeedHunger);
        Assert.Equal(61.2, hunger.Value, 1);

        var wallet = facts.Single(f => f.Code == CauseCodes.WalletBalance);
        Assert.Equal(1_234, wallet.Value);
        Assert.Equal("cents", wallet.Unit);

        var ledgerFact = facts.Single(f => f.Code == CauseCodes.LedgerEntry);
        Assert.Equal(-320, ledgerFact.Value);
        Assert.Equal("ConsumerSpending", ledgerFact.Subject);
        Assert.Equal(1_050, ledgerFact.SinceTick);
    }

    [Fact]
    public void Build_states_homelessness_as_a_raw_fact_and_invents_no_tenure()
    {
        var world = World.Create();
        var entity = world.Create(
            new NetworkId(7UL),
            new GenName("Bo"),
            new Needs());

        var facts = CauseReadModel.Build(world, entity, 500, ledger: null);

        var homeless = facts.Single(f => f.Code == CauseCodes.HousingHomeless);
        Assert.Equal(1, homeless.Value);
        Assert.Equal("flag", homeless.Unit);
        Assert.DoesNotContain(facts, f => f.Code == CauseCodes.HousingTenure);
        Assert.DoesNotContain(facts, f => f.Code == CauseCodes.LedgerEntry);
    }

    [Fact]
    public void Build_uses_the_contract_code_set_only()
    {
        var contractCodes = typeof(CauseCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

        var world = World.Create();
        var entity = world.Create(
            new NetworkId(9UL),
            new GenName("Cy"),
            new Needs(),
            new NutritionState { LastMealTick = 10 },
            new Wallet(Money.FromCents(10)));

        var facts = CauseReadModel.Build(world, entity, 20, ledger: null);

        Assert.NotEmpty(facts);
        Assert.All(facts, fact => Assert.Contains(fact.Code, contractCodes));
    }
}
