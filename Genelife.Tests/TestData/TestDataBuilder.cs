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
        decimal? revenue = null,
        decimal? taxRate = null,
        List<Guid>? employeeIds = null,
        CompanyType? type = null,
        int? minEmployees = null,
        int? maxEmployees = null)
    {
        return new Company(
            name ?? Faker.Company.CompanyName(),
            revenue ?? Faker.Random.Decimal(100000, 10000000),
            taxRate ?? Faker.Random.Decimal(0.15m, 0.35m),
            employeeIds ?? new List<Guid>(),
            type ?? Faker.PickRandom<CompanyType>(),
            minEmployees ?? Faker.Random.Int(1, 10),
            maxEmployees ?? Faker.Random.Int(20, 100)
        );
    }

    public static JobPosting CreateJobPosting(
        Guid? companyId = null,
        string? title = null,
        string? description = null,
        List<string>? requirements = null,
        decimal? salaryMin = null,
        decimal? salaryMax = null,
        JobLevel? level = null,
        CompanyType? industry = null,
        DateTime? postedDate = null,
        DateTime? expiryDate = null,
        JobPostingStatus? status = null,
        int? maxApplications = null)
    {
        var minSalary = salaryMin ?? Faker.Random.Decimal(30000, 80000);
        var maxSalary = salaryMax ?? minSalary + Faker.Random.Decimal(10000, 50000);
        
        return new JobPosting(
            companyId ?? Guid.NewGuid(),
            title ?? Faker.Name.JobTitle(),
            description ?? Faker.Lorem.Paragraph(),
            requirements ?? Faker.Make(3, () => Faker.Hacker.Noun()).ToList(),
            minSalary,
            maxSalary,
            level ?? Faker.PickRandom<JobLevel>(),
            industry ?? Faker.PickRandom<CompanyType>(),
            postedDate ?? Faker.Date.Recent(30),
            expiryDate,
            status ?? JobPostingStatus.Active,
            maxApplications ?? Faker.Random.Int(50, 200)
        );
    }

    public static JobApplication CreateJobApplication(
        Guid? jobPostingId = null,
        Guid? humanId = null,
        DateTime? applicationDate = null,
        ApplicationStatus? status = null,
        decimal? requestedSalary = null,
        string? coverLetter = null,
        List<string>? skills = null,
        int? yearsOfExperience = null,
        decimal? matchScore = null)
    {
        return new JobApplication(
            jobPostingId ?? Guid.NewGuid(),
            humanId ?? Guid.NewGuid(),
            applicationDate ?? Faker.Date.Recent(7),
            status ?? ApplicationStatus.Submitted,
            requestedSalary ?? Faker.Random.Decimal(40000, 120000),
            coverLetter ?? Faker.Lorem.Paragraph(),
            skills ?? Faker.Make(5, () => Faker.Hacker.Noun()).ToList(),
            yearsOfExperience ?? Faker.Random.Int(0, 20),
            matchScore ?? Faker.Random.Decimal(0, 1)
        );
    }

    public static Employee CreateEmployee(
        Guid? humanId = null,
        Guid? companyId = null,
        decimal? salary = null,
        DateTime? hireDate = null,
        EmploymentStatus? status = null,
        decimal? productivityScore = null)
    {
        return new Employee(
            humanId ?? Guid.NewGuid(),
            companyId ?? Guid.NewGuid(),
            salary ?? Faker.Random.Decimal(30000, 150000),
            hireDate ?? Faker.Date.Past(5),
            status ?? EmploymentStatus.Active,
            productivityScore ?? Faker.Random.Decimal(0.5m, 1.5m)
        );
    }

    public static Employment CreateEmployment(
        List<string>? skills,
        int? yearsOfExperience,
        Guid currentEmployerId,
        decimal? currentSalary = null,
        EmploymentStatus? employmentStatus = null,
        DateTime? lastJobSearchDate = null,
        bool? isActivelyJobSeeking = null)
    {
        return new Employment(
            skills ?? Faker.Make(5, () => Faker.Hacker.Noun()).ToList(),
            yearsOfExperience ?? Faker.Random.Int(0, 20),
            currentEmployerId,
            [],
            currentSalary,
            employmentStatus ?? EmploymentStatus.Unemployed,
            lastJobSearchDate,
            isActivelyJobSeeking ?? true
        );
    }
}