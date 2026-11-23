using Arch.Core;
using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.UnitTests.Repositories;

public class EconomyRepositoriesTests
{
    [Fact]
    public void CompanyRepository_SeedsAccountAndHeadquarters()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var networkIds = new NetworkIdRegistry();
        var companies = new CompanyRepository(world, economy.Employment, networkIds, economy);

        var id = companies.Add("Acme");
        var status = companies.Get(id);

        Assert.NotNull(status);
        Assert.Equal(Money.FromUnits(10_000m).Units, status!.Balance);
        Assert.Equal(1, status.OfficeCount);
        Assert.Equal(CompanySolvency.Healthy.ToString(), status.Solvency);
    }

    [Fact]
    public void OfficeRepository_RejectsCapacityBelowUsedSeatsAndCloseWithEmployees()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var networkIds = new NetworkIdRegistry();
        var companies = new CompanyRepository(world, economy.Employment, networkIds, economy);
        var offices = new OfficeRepository(world, networkIds, economy);
        var companyId = companies.Add("Acme", createHeadquarters: false);

        networkIds.TryGetEntity(companyId, out var company);
        var officeId = offices.Add(companyId, "Main", 2, Money.FromUnits(50m));
        networkIds.TryGetEntity(officeId, out var office);
        var sim = world.Create(new GenName("Worker"), new Employee(company, office, "Dev", Money.FromUnits(1000m)));
        var sim2 = world.Create(new GenName("Worker2"), new Employee(company, office, "Dev", Money.FromUnits(1000m)));

        Assert.Throws<InvalidOperationException>(() => offices.Update(officeId, null, 1, null));
        Assert.Throws<InvalidOperationException>(() => offices.Close(officeId));

        world.Remove<Employee>(sim);
        world.Remove<Employee>(sim2);
        Assert.True(offices.Close(officeId));
        Assert.True(world.Get<Office>(office).IsClosed);
    }

    [Fact]
    public void JobPostingRepository_RespectsOfficeCapacityAndClosesCleanly()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var networkIds = new NetworkIdRegistry();
        var companies = new CompanyRepository(world, economy.Employment, networkIds, economy);
        var offices = new OfficeRepository(world, networkIds, economy);
        var postings = new JobPostingRepository(world, networkIds, economy);

        var companyId = companies.Add("Acme", createHeadquarters: false);
        var officeId = offices.Add(companyId, "Main", 3, Money.FromUnits(50m));

        Assert.Throws<InvalidOperationException>(() => postings.Open(
            companyId,
            officeId,
            "Dev",
            Money.FromUnits(1000m),
            Skill.Programming,
            seats: 4));

        var postingId = postings.Open(
            companyId,
            officeId,
            "Dev",
            Money.FromUnits(1000m),
            Skill.Programming,
            seats: 2);

        networkIds.TryGetEntity(officeId, out var office);
        Assert.Equal(2, world.Get<Office>(office).SeatsUsed);

        networkIds.TryGetEntity(postingId, out var posting);
        var applicant = world.Create(new GenName("Applicant"));
        world.Create(new JobApplication(applicant, posting, "Dev", world.Get<JobPosting>(posting).Company, 0.9f));

        Assert.True(postings.Close(postingId));
        Assert.False(world.IsAlive(posting));
        Assert.Equal(0, world.Get<Office>(office).SeatsUsed);

        var applications = new List<Entity>();
        var appQuery = new QueryDescription().WithAll<JobApplication>();
        world.Query(in appQuery, (Entity entity, ref JobApplication app) => applications.Add(entity));
        Assert.All(applications, entity =>
            Assert.Equal(ApplicationStatus.Rejected, world.Get<JobApplication>(entity).Status));
    }

    [Fact]
    public void EmploymentRepository_Fire_ReopensExactlyOneSeat()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var networkIds = new NetworkIdRegistry();
        var companies = new CompanyRepository(world, economy.Employment, networkIds, economy);
        var offices = new OfficeRepository(world, networkIds, economy);
        var postings = new JobPostingRepository(world, networkIds, economy);
        var employment = new EmploymentRepository(world, economy.Employment, networkIds, economy);

        var companyId = companies.Add("Acme", createHeadquarters: false);
        var officeId = offices.Add(companyId, "Main", 2, Money.FromUnits(50m));
        var postingId = postings.Open(
            companyId,
            officeId,
            "Dev",
            Money.FromUnits(1000m),
            Skill.Programming,
            seats: 1);

        networkIds.TryGetEntity(postingId, out var posting);
        var seats = new List<Entity>();
        world.Query(in new QueryDescription().WithAll<OpenPosition>(),
            (Entity entity, ref OpenPosition _) => seats.Add(entity));
        foreach (var seat in seats)
            world.Destroy(seat);

        ref var postingData = ref world.Get<JobPosting>(posting);
        postingData.OpenSeats = 0;
        postingData.IsClosed = true;

        networkIds.TryGetEntity(companyId, out var company);
        networkIds.TryGetEntity(officeId, out var office);
        var sim = world.Create(
            new GenName("Worker"),
            new Employee(company, office, "Dev", Money.FromUnits(1000m)));
        var simId = networkIds.Allocate(sim);

        Assert.True(employment.Fire(simId.Value, 100));

        Assert.Equal(1, world.Get<JobPosting>(posting).OpenSeats);
        Assert.False(world.Get<JobPosting>(posting).IsClosed);

        var openSeats = 0;
        world.Query(in new QueryDescription().WithAll<OpenPosition>(),
            (ref OpenPosition _) => openSeats++);
        Assert.Equal(1, openSeats);
    }
}
