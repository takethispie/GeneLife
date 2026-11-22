using Arch.Core;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Enums;
using Genelife.Systems;
using Genelife.Values;

namespace Genelife.UnitTests.Systems;

public class HiringSystemTests
{
    private static (World World, Entity Company, Entity Office, Entity Posting) SetupPosting(
        World world, string position = "Dev", decimal salary = 3000m, Genelife.Enums.Skill requirements = Genelife.Enums.Skill.None)
    {
        var company = world.Create(new CompanyName("Acme"));
        var office = world.Create(new Office("HQ", company));
        var posting = world.Create(new JobPosting(company, office, position, Money.FromUnits(salary), requirements));
        return (world, company, office, posting);
    }

    private static Entity AddSeeker(World world, int age = 20, Genelife.Enums.Skill skills = Genelife.Enums.Skill.None, decimal desiredSalary = 1000m)
    {
        return world.Create(
            new Alive(age),
            new JobSeeker(true, skills, Money.FromUnits(desiredSalary))
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

        var system = new HiringSystem(world, index, 0);
        system.Update();

        var query = new QueryDescription().WithAll<JobApplication>();
        int count = 0;
        world.Query(in query, (ref JobApplication app) => count++);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Update_StoresPostingDetailsOnApplication()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        var (_, company, _, posting) = SetupPosting(world);
        world.Create(new OpenPosition(posting));
        AddSeeker(world);

        var system = new HiringSystem(world, index, 0);
        system.Update();

        var applications = new List<JobApplication>();
        var query = new QueryDescription().WithAll<JobApplication>();
        world.Query(in query, (ref JobApplication app) => applications.Add(app));

        var application = Assert.Single(applications);
        Assert.Equal("Dev", application.Position);
        Assert.Equal(company, application.Company);
    }

    [Fact]
    public void Update_PreservesApplicationDetailsAfterPostingIsDestroyed()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        var (_, company, _, posting) = SetupPosting(world);
        world.Create(new OpenPosition(posting));
        AddSeeker(world);

        var system = new HiringSystem(world, index, 0);
        system.Update();
        system.Update();

        Assert.False(world.IsAlive(posting));

        var applications = new List<JobApplication>();
        var query = new QueryDescription().WithAll<JobApplication>();
        world.Query(in query, (ref JobApplication app) => applications.Add(app));

        var application = Assert.Single(applications);
        Assert.Equal("Dev", application.Position);
        Assert.Equal(company, application.Company);
    }

    [Fact]
    public void Update_HiresApplicantAndRemovesJobSeeker()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        var (_, company, _, posting) = SetupPosting(world);
        world.Create(new OpenPosition(posting));
        var seeker = AddSeeker(world);

        var system = new HiringSystem(world, index, 0);

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

        var system = new HiringSystem(world, index, 0);
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

        var system = new HiringSystem(world, index, 0);
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

        var system = new HiringSystem(world, index, 0);
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

        var system = new HiringSystem(world, index, 0);
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
        var (_, _, _, posting) = SetupPosting(world, salary: 10m, requirements: Genelife.Enums.Skill.Rare);
        world.Create(new OpenPosition(posting));
        AddSeeker(world, skills: Genelife.Enums.Skill.None, desiredSalary: 100000m);

        var system = new HiringSystem(world, index, 0);
        system.Update();

        var query = new QueryDescription().WithAll<JobApplication>();
        int count = 0;
        world.Query(in query, (ref JobApplication app) => count++);
        Assert.Equal(0, count);
    }
}