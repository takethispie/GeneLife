using Arch.Core;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Enums;
using Genelife.Systems;

namespace Genelife.UnitTests.Systems;

public class HiringSystemTests
{
    private static (World World, Entity Company, Entity Office, Entity Posting) SetupPosting(
        World world, string position = "Dev", float salary = 3000f, List<string>? requirements = null)
    {
        var company = world.Create(new CompanyName("Acme"));
        var office = world.Create(new Office("HQ", company));
        var posting = world.Create(new JobPosting(company, office, position, salary, requirements));
        return (world, company, office, posting);
    }

    private static Entity AddSeeker(World world, int age = 20, List<string>? skills = null, float desiredSalary = 1000f)
    {
        return world.Create(
            new Alive(age),
            new JobSeeker(true, skills, desiredSalary)
        );
    }

    [Fact]
    public void Update_CreatesApplicationForSeeker()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        var (_, _, _, posting) = SetupPosting(world);
        world.Create(new OpenPosition(posting));
        AddSeeker(world);

        var system = new HiringSystem(world, index);
        system.Update();

        var query = new QueryDescription().WithAll<JobApplication>();
        int count = 0;
        world.Query(in query, (ref JobApplication app) => count++);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Update_HiresApplicantAndRemovesJobSeeker()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        var (_, company, _, posting) = SetupPosting(world);
        world.Create(new OpenPosition(posting));
        var seeker = AddSeeker(world);

        var system = new HiringSystem(world, index);

        // First update: seeker applies
        system.Update();
        // Second update: application is evaluated and hired
        system.Update();

        Assert.True(world.Has<Employee>(seeker));
        Assert.False(world.Has<JobSeeker>(seeker));
        ref var employee = ref world.Get<Employee>(seeker);
        Assert.Equal(company, employee.Company);
    }

    [Fact]
    public void Update_UpdatesEmploymentIndexOnHire()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        var (_, company, _, posting) = SetupPosting(world);
        world.Create(new OpenPosition(posting));
        var seeker = AddSeeker(world);

        var system = new HiringSystem(world, index);
        system.Update();
        system.Update();

        Assert.Equal(1, index.CountEmployees(company));
    }

    [Fact]
    public void Update_DoesNotHireWhenNoOpenPositions()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        var (_, _, _, posting) = SetupPosting(world);
        // No OpenPosition entity created
        var seeker = AddSeeker(world);

        var system = new HiringSystem(world, index);
        system.Update();
        system.Update();

        Assert.False(world.Has<Employee>(seeker));
    }

    [Fact]
    public void Update_RejectsApplicationWhenNoSeatAvailable()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        var (_, _, _, posting) = SetupPosting(world);
        world.Create(new OpenPosition(posting));
        AddSeeker(world);
        AddSeeker(world);

        var system = new HiringSystem(world, index);
        system.Update(); // both apply
        system.Update(); // one hired, one rejected

        // Exactly one employee
        var employeeQuery = new QueryDescription().WithAll<Employee>();
        int employeeCount = 0;
        world.Query(in employeeQuery, (ref Employee e) => employeeCount++);
        Assert.Equal(1, employeeCount);

        // One application accepted, one rejected
        var appQuery = new QueryDescription().WithAll<JobApplication>();
        int accepted = 0, rejected = 0;
        world.Query(in appQuery, (ref JobApplication app) =>
        {
            if (app.Status == ApplicationStatus.Accepted) accepted++;
            if (app.Status == ApplicationStatus.Rejected) rejected++;
        });
        Assert.Equal(1, accepted);
        Assert.Equal(1, rejected);
    }

    [Fact]
    public void Update_DestroysFilledPosting()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        var (_, _, _, posting) = SetupPosting(world);
        world.Create(new OpenPosition(posting));
        AddSeeker(world);

        var system = new HiringSystem(world, index);
        system.Update();
        system.Update();

        Assert.False(world.IsAlive(posting));
    }

    [Fact]
    public void Update_DoesNotApplyWhenScoreTooLow()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        // Requirements that the seeker cannot match, and low salary
        var (_, _, _, posting) = SetupPosting(world, salary: 10f, requirements: new List<string> { "RareSkill" });
        world.Create(new OpenPosition(posting));
        AddSeeker(world, skills: new List<string>(), desiredSalary: 100000f);

        var system = new HiringSystem(world, index);
        system.Update();

        var query = new QueryDescription().WithAll<JobApplication>();
        int count = 0;
        world.Query(in query, (ref JobApplication app) => count++);
        Assert.Equal(0, count);
    }
}