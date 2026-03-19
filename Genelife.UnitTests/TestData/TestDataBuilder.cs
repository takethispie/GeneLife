using Bogus;
using Genelife.Domain;
using Genelife.Domain.Human;
using Genelife.Domain.Work;
using Genelife.Domain.Work.Accounting;
using Genelife.Domain.Work.Employee;
using Genelife.Domain.Work.Job;
using Genelife.Domain.Work.Skills;
using Person = Genelife.Domain.Human.Person;

namespace Genelife.UnitTests.TestData;

public static class TestDataBuilder
{
    private static readonly Faker faker = new();

    public static Person CreateHuman(
        Guid? id = null,
        string? firstName = null,
        string? lastName = null,
        DateTime? birthday = null,
        Sex? sex = null,
        float? money = null,
        float? hunger = null,
        float? energy = null,
        float? hygiene = null)
    {
        return new Person(
            id ?? Guid.NewGuid(),
            firstName ?? faker.Name.FirstName(),
            lastName ?? faker.Name.LastName(),
            birthday ?? faker.Date.Past(50, DateTime.Now.AddYears(-18)),
            sex ?? faker.PickRandom<Sex>(),
            new LifeSkillSet(),
            new Position(0, 0, 0),
            new(),
            money ?? faker.Random.Float(0, 10000),
            hunger ?? faker.Random.Float(0, 100),
            energy ?? faker.Random.Float(0, 100),
            hygiene ?? faker.Random.Float(0, 100)
        );
    }

    public static Company CreateCompany(
        Guid? companyId = null,
        string? name = null,
        float? revenue = null,
        float? taxRate = null,
        List<Guid>? employeeIds = null,
        CompanyType? type = null,
        int? minEmployees = null,
        int? maxEmployees = null)
    {
        return new Company(
            companyId ?? Guid.NewGuid(),
            name ?? faker.Company.CompanyName(),
            new AccountingDepartment(
                revenue ?? faker.Random.Float(100000f, 10000000f),
                taxRate ?? faker.Random.Float(0.15f, 0.35f)
            ),
            type ?? faker.PickRandom<CompanyType>()
        );
    }

    public static JobPosting CreateJobPosting(
        Guid? companyId = null,
        string? title = null,
        float? salaryMin = null,
        float? salaryMax = null,
        JobLevel? level = null,
        CompanyType? industry = null,
        int? maxApplications = null)
    {
        var minSalary = salaryMin ?? faker.Random.Float(30000, 80000);
        var maxSalary = salaryMax ?? minSalary + faker.Random.Float(10000, 50000);

        return new JobPosting(
            companyId ?? Guid.NewGuid(),
            title ?? faker.Name.JobTitle(),
            minSalary,
            maxSalary,
            industry ?? faker.PickRandom<CompanyType>(),
            level ?? faker.PickRandom<JobLevel>(),
            new SkillSet()
            {
                TechnicalSkills =
                {
                    TechnicalSkill.Agile,
                    TechnicalSkill.Angular,
                    TechnicalSkill.Cicd,
                    TechnicalSkill.Git
                }
            },
            new Position(0, 0, 0),
            maxApplications ?? faker.Random.Int(50, 200)
        );
    }

    public static JobApplication CreateJobApplication(
        SkillSet skillSet,
        Guid? jobPostingId = null,
        Guid? humanId = null,
        DateTime? applicationDate = null,
        float? requestedSalary = null,
        int? yearsOfExperience = null,
        float? matchScore = null)
    {
        return new JobApplication(
            jobPostingId ?? Guid.NewGuid(),
            humanId ?? Guid.NewGuid(),
            applicationDate ?? faker.Date.Recent(7),
            requestedSalary ?? faker.Random.Float(40000, 120000),
            new SkillSet()
            {
                TechnicalSkills =
                {
                    TechnicalSkill.Agile,
                    TechnicalSkill.Angular,
                    TechnicalSkill.Cicd,
                    TechnicalSkill.Git
                }
            },
            yearsOfExperience ?? faker.Random.Int(0, 20),
            matchScore ?? faker.Random.Float(0, 1)
        );
    }

    public static Employee CreateEmployee(
        Guid? humanId = null,
        float? salary = null,
        DateTime? hireDate = null,
        EmploymentStatus? status = null,
        float? productivityScore = null)
    {
        return new Employee(
            humanId ?? Guid.NewGuid(),
            salary ?? faker.Random.Float(30000, 150000),
            hireDate ?? faker.Date.Past(5),
            status ?? EmploymentStatus.Active,
            productivityScore ?? faker.Random.Float(0.5f, 1.5f)
        );
    }

    public static Employment CreateEmployment(
        List<string>? skills,
        int? yearsOfExperience,
        Guid currentEmployerId,
        float? currentSalary = null,
        EmploymentStatus? employmentStatus = null,
        DateTime? lastJobSearchDate = null,
        bool? isActivelyJobSeeking = null)
    {
        return new Employment(
            skills ?? faker.Make(5, () => faker.Hacker.Noun()).ToList(),
            yearsOfExperience ?? faker.Random.Int(0, 20),
            currentEmployerId,
            currentSalary,
            employmentStatus ?? EmploymentStatus.Unemployed,
            lastJobSearchDate,
            isActivelyJobSeeking ?? false);
    }
}