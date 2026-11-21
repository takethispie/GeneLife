using Arch.Core;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Systems;

namespace Genelife.UnitTests.Systems;

public class JobSeekerSystemTests
{
    [Fact]
    public void Update_MakesAdultAJobSeeker()
    {
        var world = World.Create();
        var entity = world.Create(new Alive(20));
        var system = new JobSeekerSystem(world);

        system.Update();

        Assert.True(world.Has<JobSeeker>(entity));
        ref var seeker = ref world.Get<JobSeeker>(entity);
        Assert.True(seeker.IsLookingForWork);
    }

    [Fact]
    public void Update_DoesNotMakeMinorAJobSeeker()
    {
        var world = World.Create();
        var entity = world.Create(new Alive(10));
        var system = new JobSeekerSystem(world);

        system.Update();

        Assert.False(world.Has<JobSeeker>(entity));
    }

    [Fact]
    public void Update_DoesNotMakeEmployedSimAJobSeeker()
    {
        var world = World.Create();
        var company = world.Create();
        var office = world.Create();
        var entity = world.Create(
            new Alive(25),
            new Employee(company, office, "Dev", 3000f)
        );
        var system = new JobSeekerSystem(world);

        system.Update();

        Assert.False(world.Has<JobSeeker>(entity));
    }

    [Fact]
    public void Update_DoesNotDuplicateJobSeeker()
    {
        var world = World.Create();
        var entity = world.Create(new Alive(20));
        var system = new JobSeekerSystem(world);

        system.Update();
        system.Update();

        Assert.True(world.Has<JobSeeker>(entity));
    }
}