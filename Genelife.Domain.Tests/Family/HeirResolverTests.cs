using Arch.Core;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Family;
using Genelife.Values;

namespace Genelife.UnitTests.Family;

public class HeirResolverTests
{
    [Fact]
    public void PartnerAndChildrenShareTheEstate()
    {
        var context = new Context();
        var deceased = context.Sim(60, 1);
        var partner = context.Sim(58, 2);
        var adultChild = context.Sim(35, 3);
        var minorChild = context.Sim(10, 4);
        context.Partners(deceased, partner);
        context.Parent(deceased, adultChild);
        context.Parent(deceased, minorChild);

        var plan = HeirResolver.Resolve(context.World, context.Index, deceased);

        Assert.Equal(1m, plan.Allocations.Sum(allocation => allocation.Share));
        Assert.Equal(partner, plan.Allocations.Single(allocation => allocation.Tier == "partner").Heir);
        Assert.Equal(2, plan.Allocations.Count(allocation => allocation.Tier == "child"));
        Assert.Equal(0.25m, plan.Allocations.Single(allocation => allocation.Heir == adultChild).Share);
        Assert.Equal(0.25m, plan.Allocations.Single(allocation => allocation.Heir == minorChild).Share);
    }

    [Fact]
    public void ChildrenTakeEverythingWhenThereIsNoPartner()
    {
        var context = new Context();
        var deceased = context.Sim(60, 1);
        var first = context.Sim(30, 2);
        var second = context.Sim(28, 3);
        context.Parent(deceased, first);
        context.Parent(deceased, second);

        var plan = HeirResolver.Resolve(context.World, context.Index, deceased);

        Assert.Equal(2, plan.Allocations.Count);
        Assert.All(plan.Allocations, allocation => Assert.Equal("child", allocation.Tier));
        Assert.Equal(1m, plan.Allocations.Sum(allocation => allocation.Share));
    }

    [Fact]
    public void PartnerWithoutChildrenLeavesResidualToParentsOrSiblings()
    {
        var context = new Context();
        var deceased = context.Sim(60, 1);
        var partner = context.Sim(58, 2);
        var parent = context.Sim(85, 3);
        var sibling = context.Sim(55, 4);
        context.Partners(deceased, partner);
        context.Parent(parent, deceased);
        context.Parent(parent, sibling);

        var plan = HeirResolver.Resolve(context.World, context.Index, deceased);

        var partnerShare = plan.Allocations.Single(allocation => allocation.Heir == partner).Share;
        var parentShare = plan.Allocations.Single(allocation => allocation.Heir == parent).Share;
        Assert.Equal(0.5m, partnerShare);
        Assert.Equal(0.5m, parentShare);
        Assert.Equal(1m, plan.Allocations.Sum(allocation => allocation.Share));
    }

    [Fact]
    public void NoRelativesYieldsNoAllocations()
    {
        var context = new Context();
        var deceased = context.Sim(60, 1);

        var plan = HeirResolver.Resolve(context.World, context.Index, deceased);

        Assert.Empty(plan.Allocations);
    }

    [Fact]
    public void ExPartnerCountsAsSurvivingPartner()
    {
        var context = new Context();
        var deceased = context.Sim(60, 1);
        var partner = context.Sim(58, 2);
        var child = context.Sim(20, 3);
        context.Partners(deceased, partner);
        context.Parent(deceased, child);
        context.Service.EndPartnership(deceased, partner, 10);

        var plan = HeirResolver.Resolve(context.World, context.Index, deceased);

        Assert.Contains(plan.Allocations, allocation =>
            allocation.Heir == partner && allocation.Tier == "partner");
        Assert.Contains(plan.Allocations, allocation =>
            allocation.Heir == child && allocation.Tier == "child");
    }

    private sealed class Context
    {
        public World World { get; } = World.Create();
        public RelationshipIndex Index { get; } = new();
        public RelationshipService Service { get; }

        public Context()
        {
            Service = new RelationshipService(World, Index);
        }

        public Entity Sim(int age, ulong networkId) =>
            World.Create(new Alive(age), new NetworkId(networkId));

        public void Partners(Entity first, Entity second) =>
            Service.CreatePair(first, second, RelationshipKind.Partner, RelationshipSource.Seed, 1);

        public void Parent(Entity parent, Entity child) =>
            Service.CreatePair(parent, child, RelationshipKind.Parent, RelationshipSource.Seed, 1);
    }
}
