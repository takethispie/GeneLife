using Bogus;
using Genelife.Domain;
using Genelife.Domain.Work;

namespace Genelife.Tests.TestData;

public static class TestDataBuilder
{
    private static readonly Faker Faker = new();

    public static Human CreateHuman(
        string? firstName = null,
        string? lastName = null,
        DateTime? birthday = null,
        Sex? sex = null,
        float? money = null,
        float? hunger = null,
        float? energy = null,
        float? hygiene = null)
    {
        return new Human(
            firstName ?? Faker.Name.FirstName(),
            lastName ?? Faker.Name.LastName(),
            birthday ?? Faker.Date.Past(50, DateTime.Now.AddYears(-18)),
            sex ?? Faker.PickRandom<Sex>(),
            money ?? Faker.Random.Float(0, 10000),
            hunger ?? Faker.Random.Float(0, 100),
            energy ?? Faker.Random.Float(0, 100),
            hygiene ?? Faker.Random.Float(0, 100)
        );
    }

    public static Company CreateCompany(
        string? name = null,
        float? revenue = null,
        float? taxRate = null,
        List<Guid>? employeeIds = null,
        CompanyType? type = null,
        int? minEmployees = null,
        int? maxEmployees = null)
    {
        return new Company(
            name ?? Faker.Company.CompanyName(),
            revenue ?? Faker.Random.Float(100000f, 10000000f),
            taxRate ?? Faker.Random.Float(0.15f, 0.35f),
            employeeIds ?? new List<Guid>(),
            type ?? Faker.PickRandom<CompanyType>(),
            minEmployees ?? Faker.Random.Int(1, 10),
            maxEmployees ?? Faker.Random.Int(20, 100)
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
        var minSalary = salaryMin ?? Faker.Random.Float(30000, 80000);
        var maxSalary = salaryMax ?? minSalary + Faker.Random.Float(10000, 50000);
        
        return new JobPosting(
            companyId ?? Guid.NewGuid(),
            title ?? Faker.Name.JobTitle(),
            minSalary,
            maxSalary,
            industry ?? Faker.PickRandom<CompanyType>(),
            level ?? Faker.PickRandom<JobLevel>(),
            maxApplications ?? Faker.Random.Int(50, 200)
        );
    }

    public static JobApplication CreateJobApplication(
        Guid? jobPostingId = null,
        Guid? humanId = null,
        DateTime? applicationDate = null,
        ApplicationStatus? status = null,
        float? requestedSalary = null,
        List<string>? skills = null,
        int? yearsOfExperience = null,
        float? matchScore = null)
    {
        return new JobApplication(
            jobPostingId ?? Guid.NewGuid(),
            humanId ?? Guid.NewGuid(),
            applicationDate ?? Faker.Date.Recent(7),
            status ?? ApplicationStatus.Submitted,
            requestedSalary ?? Faker.Random.Float(40000, 120000),
            skills ?? Faker.Make(5, () => Faker.Hacker.Noun()).ToList(),
            yearsOfExperience ?? Faker.Random.Int(0, 20),
            matchScore ?? Faker.Random.Float(0, 1)
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
            salary ?? Faker.Random.Float(30000, 150000),
            hireDate ?? Faker.Date.Past(5),
            status ?? EmploymentStatus.Active,
            productivityScore ?? Faker.Random.Float(0.5f, 1.5f)
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
            skills ?? Faker.Make(5, () => Faker.Hacker.Noun()).ToList(),
            yearsOfExperience ?? Faker.Random.Int(0, 20),
            currentEmployerId,
            currentSalary,
            employmentStatus ?? EmploymentStatus.Unemployed,
            lastJobSearchDate,
            isActivelyJobSeeking ?? true
        );
    }
}