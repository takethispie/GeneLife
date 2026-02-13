using Bogus;
using Genelife.Work.Messages.DTOs;
using Genelife.Work.Messages.DTOs.Skills;

namespace Genelife.Work.Tests;

public static class TestDataBuilder
{
    private static readonly Faker Faker = new();

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
            Faker.Random.Float()
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
            new SkillSet()
            {
                TechnicalSkills =
                {
                    TechnicalSkill.Agile,
                    TechnicalSkill.Angular,
                    TechnicalSkill.CICD,
                    TechnicalSkill.Git
                }
            },
            Guid.Empty,
            new OfficeLocation(0, 0, 0),
            maxApplications ?? Faker.Random.Int(50, 200)
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
            applicationDate ?? Faker.Date.Recent(7),
            requestedSalary ?? Faker.Random.Float(40000, 120000),
            new SkillSet() {
                TechnicalSkills = {
                    TechnicalSkill.Agile,
                    TechnicalSkill.Angular,
                    TechnicalSkill.CICD,
                    TechnicalSkill.Git
                }
            },
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