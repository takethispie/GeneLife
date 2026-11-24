using Arch.Core;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.UnitTests.Components;

public class ComponentTests
{
    [Fact]
    public void Needs_DefaultsToFull()
    {
        var needs = new Needs();
        Assert.Equal(100f, needs.Hunger);
        Assert.Equal(100f, needs.Energy);
        Assert.Equal(100f, needs.Hygiene);
        Assert.Equal(100f, needs.Bladder);
    }

    [Fact]
    public void CurrentAction_StoresTypeAndDuration()
    {
        var action = new CurrentAction(ActionType.Sleeping, 8f);
        Assert.Equal(ActionType.Sleeping, action.Type);
        Assert.Equal(8f, action.TimeRemaining);
    }

    [Fact]
    public void Wallet_DefaultsToZero()
    {
        var wallet = new Wallet();
        Assert.Equal(Money.Zero, wallet.Balance);
    }

    [Fact]
    public void Wallet_StoresBalance()
    {
        var wallet = new Wallet(Money.FromUnits(123.45m));
        Assert.Equal(Money.FromUnits(123.45m), wallet.Balance);
    }

    [Fact]
    public void Alive_DefaultConstructor_LeavesAgeZero()
    {
        var alive = new Alive();
        Assert.Equal(0, alive.Age);
    }

    [Fact]
    public void Alive_StoresAge()
    {
        var alive = new Alive(42);
        Assert.Equal(42, alive.Age);
    }

    [Fact]
    public void GenName_StoresName()
    {
        var name = new GenName("Alice");
        Assert.Equal("Alice", name.Name);
    }

    [Fact]
    public void Location_StoresCoordinates()
    {
        var loc = new Location(new System.Numerics.Vector2(1f, 2f));
        Assert.Equal(1f, loc.Position.X);
        Assert.Equal(2f, loc.Position.Y);
    }

    [Fact]
    public void Location_ConstructorWithFloats_StoresCoordinates()
    {
        var loc = new Location(3f, 4f);
        Assert.Equal(3f, loc.Position.X);
        Assert.Equal(4f, loc.Position.Y);
    }

    [Fact]
    public void CompanyName_StoresName()
    {
        var name = new CompanyName("Acme");
        Assert.Equal("Acme", name.Name);
    }

    [Fact]
    public void Office_StoresFields()
    {
        var world = World.Create();
        var company = world.Create();
        var office = new Office("HQ", company, 25);
        Assert.Equal("HQ", office.OfficeName);
        Assert.Equal(company, office.Company);
        Assert.Equal(25, office.Capacity);
    }

    [Fact]
    public void Office_DefaultsCapacityToFifty()
    {
        var world = World.Create();
        var company = world.Create();
        var office = new Office("HQ", company);
        Assert.Equal(50, office.Capacity);
    }

    [Fact]
    public void Employee_StoresFields()
    {
        var world = World.Create();
        var company = world.Create();
        var office = world.Create();
        var employee = new Employee(company, office, "Engineer", Money.FromUnits(5000m));
        Assert.Equal(company, employee.Company);
        Assert.Equal(office, employee.Office);
        Assert.Equal("Engineer", employee.Position);
        Assert.Equal(Money.FromUnits(5000m), employee.SalaryPerCycle);
    }

    [Fact]
    public void JobSeeker_DefaultConstructor_LeavesFieldsDefault()
    {
        var seeker = new JobSeeker();
        Assert.False(seeker.IsLookingForWork);
        Assert.Equal(Enums.Skill.None, seeker.Skills);
        Assert.Equal(Money.Zero, seeker.DesiredSalary);
    }

    [Fact]
    public void JobSeeker_StoresFields()
    {
        var seeker = new JobSeeker(false, Enums.Skill.Programming | Enums.Skill.Medical, Money.FromUnits(2500m));
        Assert.False(seeker.IsLookingForWork);
        Assert.Equal(Enums.Skill.Programming | Enums.Skill.Medical, seeker.Skills);
        Assert.Equal(Money.FromUnits(2500m), seeker.DesiredSalary);
    }

    [Fact]
    public void JobPosting_StoresFields()
    {
        var world = World.Create();
        var company = world.Create();
        var office = world.Create();
        var posting = new JobPosting(company, office, "Dev", Money.FromUnits(3000m), Enums.Skill.Programming);
        Assert.Equal(company, posting.Company);
        Assert.Equal(office, posting.Office);
        Assert.Equal("Dev", posting.Position);
        Assert.Equal(Money.FromUnits(3000m), posting.SalaryPerCycle);
        Assert.Equal(Enums.Skill.Programming, posting.Requirements);
    }

    [Fact]
    public void JobPosting_DefaultsRequirementsToEmpty()
    {
        var world = World.Create();
        var company = world.Create();
        var office = world.Create();
        var posting = new JobPosting(company, office, "Dev", Money.FromUnits(3000m));
        Assert.Equal(Enums.Skill.None, posting.Requirements);
    }

    [Fact]
    public void JobApplication_DefaultsToPending()
    {
        var world = World.Create();
        var applicant = world.Create();
        var posting = world.Create();
        var app = new JobApplication(applicant, posting);
        Assert.Equal(applicant, app.Applicant);
        Assert.Equal(posting, app.JobPosting);
        Assert.Equal(ApplicationStatus.Pending, app.Status);
        Assert.Equal(0f, app.MatchScore);
    }

    [Fact]
    public void JobApplication_StoresMatchScore()
    {
        var world = World.Create();
        var applicant = world.Create();
        var posting = world.Create();
        var app = new JobApplication(applicant, posting, 0.75f);
        Assert.Equal(0.75f, app.MatchScore);
    }

    [Fact]
    public void OpenPosition_StoresPosting()
    {
        var world = World.Create();
        var posting = world.Create();
        var seat = new OpenPosition(posting);
        Assert.Equal(posting, seat.Posting);
    }
}