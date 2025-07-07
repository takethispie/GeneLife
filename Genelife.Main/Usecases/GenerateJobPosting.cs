using Genelife.Domain;

namespace Genelife.Main.Usecases;

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
    
    private static readonly Dictionary<CompanyType, List<string>> SkillsByType = new()
    {
        [CompanyType.Technology] = new List<string>
        {
            "C#", "JavaScript", "Python", "React", "Angular", "Docker", "Kubernetes",
            "AWS", "Azure", "SQL", "NoSQL", "Git", "Agile", "Scrum", "REST APIs",
            "Microservices", "CI/CD", "Unit Testing", "Problem Solving", "Communication"
        },
        [CompanyType.Manufacturing] = new List<string>
        {
            "Lean Manufacturing", "Six Sigma", "Quality Control", "Safety Protocols",
            "Equipment Maintenance", "Process Improvement", "Inventory Management",
            "CAD Software", "Statistical Analysis", "Team Leadership", "Problem Solving"
        },
        [CompanyType.Services] = new List<string>
        {
            "Project Management", "Client Relations", "Business Analysis", "Excel",
            "PowerPoint", "CRM Software", "Data Analysis", "Communication", "Negotiation",
            "Strategic Planning", "Process Improvement", "Team Leadership"
        },
        [CompanyType.Retail] = new List<string>
        {
            "Customer Service", "Sales", "Inventory Management", "POS Systems",
            "Visual Merchandising", "Cash Handling", "Team Leadership", "Communication",
            "Problem Solving", "Retail Software", "Loss Prevention"
        },
        [CompanyType.Healthcare] = new List<string>
        {
            "Patient Care", "Medical Terminology", "HIPAA Compliance", "Electronic Health Records",
            "Clinical Skills", "Communication", "Attention to Detail", "Empathy",
            "Medical Equipment", "Documentation", "Team Collaboration"
        }
    };
    
    public JobPosting GenerateForCompany(Company company, JobLevel level, int positionsNeeded)
    {
        var titles = JobTitlesByType.GetValueOrDefault(company.Type, JobTitlesByType[CompanyType.Services]);
        var skills = SkillsByType.GetValueOrDefault(company.Type, SkillsByType[CompanyType.Services]);
        
        var title = titles[random.Next(titles.Count)];
        var description = GenerateJobDescription(title, company.Type, level);
        var requirements = GenerateRequirements(skills, level);
        var (salaryMin, salaryMax) = GenerateSalaryRange(company.Type, level);
        
        return new JobPosting(
            Id: Guid.NewGuid(),
            CompanyId: company.Id,
            Title: title,
            Description: description,
            Requirements: requirements,
            SalaryMin: salaryMin,
            SalaryMax: salaryMax,
            Level: level,
            Industry: company.Type,
            PostedDate: DateTime.UtcNow,
            ExpiryDate: DateTime.UtcNow.AddDays(30),
            Status: JobPostingStatus.Active,
            MaxApplications: Math.Min(100, positionsNeeded * 20) // 20 applications per position
        );
    }
    
    private string GenerateJobDescription(string title, CompanyType companyType, JobLevel level)
    {
        var experienceText = level switch
        {
            JobLevel.Entry => "entry-level position perfect for recent graduates",
            JobLevel.Junior => "junior role requiring 1-3 years of experience",
            JobLevel.Mid => "mid-level position requiring 3-6 years of experience",
            JobLevel.Senior => "senior role requiring 6+ years of experience",
            JobLevel.Lead => "leadership position requiring 8+ years of experience",
            JobLevel.Manager => "management role requiring 10+ years of experience",
            JobLevel.Director => "director-level position requiring 12+ years of experience",
            JobLevel.Executive => "executive position requiring 15+ years of experience",
            _ => "position"
        };
        
        var industryContext = companyType switch
        {
            CompanyType.Technology => "innovative technology company",
            CompanyType.Manufacturing => "established manufacturing organization",
            CompanyType.Services => "professional services firm",
            CompanyType.Retail => "dynamic retail environment",
            CompanyType.Healthcare => "healthcare organization",
            _ => "growing company"
        };
        
        return $"We are seeking a talented {title} to join our {industryContext}. " +
               $"This is an {experienceText} offering excellent growth opportunities, " +
               $"competitive compensation, and a collaborative work environment. " +
               $"The successful candidate will contribute to our team's success and help drive our mission forward.";
    }
    
    private List<string> GenerateRequirements(List<string> availableSkills, JobLevel level)
    {
        var numRequirements = level switch
        {
            JobLevel.Entry => random.Next(3, 6),
            JobLevel.Junior => random.Next(4, 7),
            JobLevel.Mid => random.Next(5, 8),
            JobLevel.Senior => random.Next(6, 10),
            JobLevel.Lead => random.Next(7, 11),
            JobLevel.Manager => random.Next(6, 9),
            JobLevel.Director => random.Next(5, 8),
            JobLevel.Executive => random.Next(4, 7),
            _ => 5
        };
        
        return availableSkills
            .OrderBy(x => random.Next())
            .Take(numRequirements)
            .ToList();
    }
    
    private (decimal min, decimal max) GenerateSalaryRange(CompanyType companyType, JobLevel level)
    {
        var baseSalary = companyType switch
        {
            CompanyType.Technology => level switch
            {
                JobLevel.Entry => 4500m,
                JobLevel.Junior => 5500m,
                JobLevel.Mid => 7000m,
                JobLevel.Senior => 9000m,
                JobLevel.Lead => 11000m,
                JobLevel.Manager => 12000m,
                JobLevel.Director => 15000m,
                JobLevel.Executive => 20000m,
                _ => 6000m
            },
            CompanyType.Healthcare => level switch
            {
                JobLevel.Entry => 4000m,
                JobLevel.Junior => 5000m,
                JobLevel.Mid => 6500m,
                JobLevel.Senior => 8500m,
                JobLevel.Lead => 10000m,
                JobLevel.Manager => 11000m,
                JobLevel.Director => 14000m,
                JobLevel.Executive => 18000m,
                _ => 5500m
            },
            CompanyType.Services => level switch
            {
                JobLevel.Entry => 3500m,
                JobLevel.Junior => 4500m,
                JobLevel.Mid => 6000m,
                JobLevel.Senior => 8000m,
                JobLevel.Lead => 9500m,
                JobLevel.Manager => 10500m,
                JobLevel.Director => 13000m,
                JobLevel.Executive => 17000m,
                _ => 4500m
            },
            CompanyType.Manufacturing => level switch
            {
                JobLevel.Entry => 3000m,
                JobLevel.Junior => 4000m,
                JobLevel.Mid => 5500m,
                JobLevel.Senior => 7000m,
                JobLevel.Lead => 8500m,
                JobLevel.Manager => 9500m,
                JobLevel.Director => 12000m,
                JobLevel.Executive => 16000m,
                _ => 4000m
            },
            CompanyType.Retail => level switch
            {
                JobLevel.Entry => 2500m,
                JobLevel.Junior => 3200m,
                JobLevel.Mid => 4500m,
                JobLevel.Senior => 6000m,
                JobLevel.Lead => 7500m,
                JobLevel.Manager => 8500m,
                JobLevel.Director => 11000m,
                JobLevel.Executive => 15000m,
                _ => 3000m
            },
            _ => 4000m
        };
        
        var variation = (decimal)(random.NextDouble() * 0.2 + 0.9); // 0.9 to 1.1 multiplier
        var adjustedBase = baseSalary * variation;
        
        var min = adjustedBase * 0.9m;
        var max = adjustedBase * 1.2m;
        
        return (Math.Round(min, 0), Math.Round(max, 0));
    }
}