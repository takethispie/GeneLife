using Arch.Core;
using Genelife.Components.Company;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Application.Systems;
using Genelife.Values;

namespace Genelife.UnitTests.Economy;

/// <summary>
/// Property guard for household cash pooling and the consumption purchase gate.
///
/// A genos that ends a monthly consumption pass with <c>FoodPurchased == false</c>
/// is gate-blocked: <c>ActionSystem.CanFulfill</c> suppresses hunger restoration for
/// the whole month, silently and with no error. Consumption is charged to one
/// genos's own wallet, but cash belongs to the household, so the ONLY correct
/// reason to leave a genos gate-blocked is that no household member holds cash
/// at all. This asserts exactly that, across randomized household compositions
/// and cash distributions — generalizing <see cref="HouseholdPoolingTests"/> from a
/// single hand-built two-person household to a sweep.
///
/// It fails on the pre-fix <c>PayerFor</c> (designated payer only, no household
/// fallback): a broke member of a solvent household is then left gate-blocked
/// while its spouse still holds cash.
/// </summary>
public class HouseholdGatePoolingPropertyTests
{
    private const int Cases = 400;

    [Fact]
    public void NoGenosIsGateBlocked_WhileItsHouseholdStillHoldsCash()
    {
        var rng = new Random(20261126);
        var blockedMembers = 0;
        var fullyFedHouseholds = 0;

        for (var c = 0; c < Cases; c++)
        {
            var size = rng.Next(1, 6);
            var balances = new Money[size];
            for (var i = 0; i < size; i++)
            {
                // Roughly half the members start broke. That is the shape the
                // pooling fallback exists for, and the one a designated-payer-only
                // implementation gets wrong; keeping positive balances >= 1 unit
                // avoids sub-cent budget rounding masquerading as a gate defect.
                balances[i] = rng.Next(2) == 0
                    ? Money.Zero
                    : Money.FromUnits(rng.Next(1, 5_000));
            }

            var (world, economy, members) = Household(size, balances);
            new ConsumerSpendingSystem(world, economy, interval: 1).Update(tick: 100);

            var household = economy.Housing.HouseholdOf(members[0]);
            var householdCash = Money.Zero;
            foreach (var member in economy.Housing.GetMembers(household))
                householdCash += world.Get<Wallet>(member).Balance;

            var anyBlocked = false;
            foreach (var member in members)
            {
                var state = world.Get<ConsumptionState>(member);
                Assert.False(state.LastSpendTick == 0,
                    $"case {c}: the monthly pass should have stamped LastSpendTick");

                if (state.FoodPurchased)
                    continue;

                anyBlocked = true;
                blockedMembers++;
                Assert.True(householdCash.IsZero,
                    $"case {c}: a genos was gate-blocked for food while its household still " +
                    $"held {householdCash} across member wallets ({Describe(balances)})");
            }

            if (!anyBlocked)
                fullyFedHouseholds++;
        }

        // Guard against a vacuous property: the sweep must actually exercise both
        // the fed and the gate-blocked outcomes, or it would pass on any code.
        Assert.True(blockedMembers > 0,
            "the sweep never produced a gate-blocked member, so the property proves nothing");
        Assert.True(fullyFedHouseholds > 0,
            "the sweep never produced a fully-fed household, so the property proves nothing");
    }

    private static (World World, EconomyContext Economy, List<Entity> Members) Household(
        int size, Money[] balances)
    {
        var world = World.Create();
        var economy = new EconomyContext(world);

        var company = world.Create(new CompanyName("Shop"), CompanyAccount.Create(Money.Zero, 0));
        world.Create(new Office("HQ", company, 10));

        var members = new List<Entity>(size);
        for (var i = 0; i < size; i++)
            members.Add(world.Create(
                new GenName($"Genos{i}"),
                new Alive(35),
                new Wallet(balances[i])));

        var household = world.Create(new Household
        {
            Head = members[0],
            Tenure = Tenure.Renter,
            MemberCount = size,
        });

        for (var i = 0; i < size; i++)
        {
            world.Add(members[i], new HouseholdMember(
                household, i == 0 ? HouseholdRole.Head : HouseholdRole.Partner));
            economy.Housing.AddMember(members[i], household);
        }

        return (world, economy, members);
    }

    private static string Describe(Money[] balances) =>
        "wallets=" + string.Join(",", balances.Select(b => b.ToString()));
}
