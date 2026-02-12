using Genelife.Work.Messages.DTOs;
using Genelife.Work.Messages.DTOs.Skills;

namespace Genelife.Work.Usecases;

public class GenerateJobPosting
{
    private readonly Random random = new();
    
    private static readonly Dictionary<CompanyType, List<string>> JobTitlesByType = new()
    {
        [CompanyType.Technology] = new List<string>
        {
            "Software Engineer", "Senior Developer", "DevOps Engineer", "Data Scientist",
            "Product Manager", "UX Designer", "System Administrator", "QA Engineer",
            "Frontend Developer", "Backend Developer", "Full Stack Developer", "Mobile Developer"
        },
        [CompanyType.Manufacturing] = new List<string>
        {
            "Production Manager", "Quality Control Specialist", "Machine Operator",
            "Industrial Engineer", "Safety Coordinator", "Maintenance Technician",
            "Supply Chain Analyst", "Operations Supervisor", "Process Engineer"
        },
        [CompanyType.Services] = new List<string>
        {
            "Business Analyst", "Project Manager", "Consultant", "Account Manager",
            "Customer Success Manager", "Sales Representative", "Marketing Specialist",
            "Operations Manager", "Financial Analyst"
        },
        [CompanyType.Retail] = new List<string>
        {
            "Store Manager", "Sales Associate", "Inventory Specialist", "Customer Service Rep",
            "Visual Merchandiser", "Buyer", "Loss Prevention Officer", "Cashier",
            "Department Supervisor"
        },
        [CompanyType.Healthcare] = new List<string>
        {
            "Nurse", "Medical Assistant", "Healthcare Administrator", "Lab Technician",
            "Physical Therapist", "Pharmacist", "Medical Records Clerk", "Radiologic Technologist",
            "Healthcare IT Specialist"
        }
    };
    
    public JobPosting GenerateForCompany(Guid companyId, CompanyType companyType, JobLevel level, int positionsNeeded,
        Guid officeId, OfficeLocation officeLocation)
    {
        var titles = JobTitlesByType.GetValueOrDefault(companyType, JobTitlesByType[CompanyType.Services]);
        var title = titles[random.Next(titles.Count)];
        var (salaryMin, salaryMax) = GenerateSalaryRange(companyType, level);

        return new JobPosting(
            CompanyId: companyId,
            Title: title,
            SalaryMin: salaryMin,
            SalaryMax: salaryMax,
            CompanyType: companyType,
            Level: level,
            // TODO add real skillset requirements
            new SkillSet(),
            officeId,
            officeLocation,
            MaxApplications: Math.Min(100, positionsNeeded * 20)
        );
    }
    
    private (float min, float max) GenerateSalaryRange(CompanyType companyType, JobLevel level)
    {
        var baseSalary = companyType switch
        {
            CompanyType.Technology => level switch
            {
                JobLevel.Entry => 2000,
                JobLevel.Junior => 3000,
                JobLevel.Mid => 5000,
                JobLevel.Senior => 8000,
                JobLevel.Lead => 10000,
                JobLevel.Manager => 12000,
                JobLevel.Director => 15000,
                JobLevel.Executive => 25000,
                _ => 6000
            },
            CompanyType.Healthcare => level switch
            {
                JobLevel.Entry => 2000,
                JobLevel.Junior => 3000,
                JobLevel.Mid => 4000,
                JobLevel.Senior => 5000,
                JobLevel.Lead => 7000,
                JobLevel.Manager => 8000,
                JobLevel.Director => 10000,
                JobLevel.Executive => 12000,
                _ => 5500
            },
            CompanyType.Services => level switch
            {
                JobLevel.Entry => 1500,
                JobLevel.Junior => 2500,
                JobLevel.Mid => 3500,
                JobLevel.Senior => 5000,
                JobLevel.Lead => 6500,
                JobLevel.Manager => 8500,
                JobLevel.Director => 10000,
                JobLevel.Executive => 12000,
                _ => 4500
            },
            CompanyType.Manufacturing => level switch
            {
                JobLevel.Entry => 1000,
                JobLevel.Junior => 2000,
                JobLevel.Mid => 2500,
                JobLevel.Senior => 3000,
                JobLevel.Lead => 4500,
                JobLevel.Manager => 5500,
                JobLevel.Director => 7000,
                JobLevel.Executive => 9000,
                _ => 2000
            },
            CompanyType.Retail => level switch
            {
                JobLevel.Entry => 1500,
                JobLevel.Junior => 2200,
                JobLevel.Mid => 3500,
                JobLevel.Senior => 4000,
                JobLevel.Lead => 7500,
                JobLevel.Manager => 8500,
                JobLevel.Director => 11000,
                JobLevel.Executive => 15000,
                _ => 3000
            },
            _ => 4000
        };
        
        var variation = random.NextSingle() * 0.2 + 0.9; // 0.9 to 1.1 multiplier
        var adjustedBase = baseSalary * variation;
        
        var min = adjustedBase * 0.9;
        var max = adjustedBase * 1.2;
        
        return (Convert.ToSingle(Math.Round(min, 0)), Convert.ToSingle(Math.Round(max, 0)));
    }
}