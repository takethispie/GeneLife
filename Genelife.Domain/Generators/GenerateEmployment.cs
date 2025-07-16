using Genelife.Domain.Work;

namespace Genelife.Domain.Generators;

public class GenerateEmployment
{
    private readonly Random random = new();
    
    private static readonly List<string> CommonSkills = new()
    {
        "Communication", "Problem Solving", "Team Work", "Leadership",
        "Time Management", "Critical Thinking", "Adaptability", "Creativity",
        "Customer Service", "Project Management", "Data Analysis", "Research",
        "Microsoft Office", "Email Management", "Presentation Skills", "Organization"
    };
    
    private static readonly List<string> TechnicalSkills = new()
    {
        "C#", "JavaScript", "Python", "React", "Angular", "Docker", "Kubernetes",
        "AWS", "Azure", "SQL", "NoSQL", "Git", "Agile", "Scrum", "REST APIs",
        "Microservices", "CI/CD", "Unit Testing", "HTML", "CSS", "Node.js"
    };
    
    private static readonly List<string> BusinessSkills = new()
    {
        "Excel", "PowerPoint", "CRM Software", "Business Analysis", "Negotiation",
        "Strategic Planning", "Process Improvement", "Financial Analysis",
        "Marketing", "Sales", "Accounting", "Budgeting", "Forecasting"
    };
    
    private static readonly List<string> HealthcareSkills = new()
    {
        "Patient Care", "Medical Terminology", "HIPAA Compliance", "Electronic Health Records",
        "Clinical Skills", "Medical Equipment", "Documentation", "Pharmacology",
        "Vital Signs", "Medical Coding", "Insurance Processing"
    };
    
    private static readonly List<string> ManufacturingSkills = new()
    {
        "Lean Manufacturing", "Six Sigma", "Quality Control", "Safety Protocols",
        "Equipment Maintenance", "Process Improvement", "Inventory Management",
        "CAD Software", "Statistical Analysis", "Machine Operation", "Welding"
    };
    
    private static readonly List<string> RetailSkills = new()
    {
        "POS Systems", "Visual Merchandising", "Cash Handling", "Inventory Management",
        "Loss Prevention", "Retail Software", "Product Knowledge", "Upselling",
        "Store Operations", "Customer Relations"
    };
    
    public Employment Execute(Human human)
    {
        // Generate years of experience based on age
        var age = DateTime.Now.Year - human.Birthday.Year;
        var maxExperience = Math.Max(0, age - 18); // Assume work starts at 18
        var experience = random.Next(0, Math.Min(maxExperience + 1, 25)); // Cap at 25 years

        return new Employment(
            GenerateSkills(experience),
            experience,
            Guid.Empty,
            Status: EmploymentStatus.Unemployed,
            IsActivelyJobSeeking: false,
            LastJobSearchDate: null
        );
    }
    
    private List<string> GenerateSkills(int experience)
    {
        var skills = new List<string>();
        
        // Always add some common skills
        var numCommonSkills = Math.Min(random.Next(3, 7), CommonSkills.Count);
        skills.AddRange(CommonSkills.OrderBy(x => random.Next()).Take(numCommonSkills));
        
        var skillCategories = new List<List<string>>();
        var allSpecializations = new List<List<string>>
        {
            TechnicalSkills, BusinessSkills, HealthcareSkills, ManufacturingSkills, RetailSkills
        };
        
        var numSpecializations = experience < 2 ? 1 : random.Next(1, 3);
        var selectedSpecializations = allSpecializations
            .OrderBy(x => random.Next())
            .Take(numSpecializations)
            .ToList();
        
        foreach (var specialization in selectedSpecializations)
        {
            var numSpecializedSkills = experience switch
            {
                < 2 => random.Next(1, 3),
                < 5 => random.Next(2, 5),
                < 10 => random.Next(3, 7),
                _ => random.Next(4, 8)
            };
            
            var specializedSkills = specialization
                .OrderBy(x => random.Next())
                .Take(Math.Min(numSpecializedSkills, specialization.Count))
                .ToList();
                
            skills.AddRange(specializedSkills);
        }
        
        return [.. skills.Distinct()];
    }
    
    public decimal GenerateDesiredSalary(Employment employmentProfile, JobPosting jobPosting)
    {
        // Base salary expectation on experience and job level
        var experienceMultiplier = 1.0m + (employmentProfile.YearsOfExperience * 0.05m); // 5% per year of experience
        
        var baseSalary = jobPosting.Level switch
        {
            JobLevel.Entry => 30000m,
            JobLevel.Junior => 40000m,
            JobLevel.Mid => 55000m,
            JobLevel.Senior => 75000m,
            JobLevel.Lead => 90000m,
            JobLevel.Manager => 100000m,
            JobLevel.Director => 130000m,
            JobLevel.Executive => 180000m,
            _ => 40000m
        };
        
        var expectedSalary = baseSalary * experienceMultiplier;
        
        // Add some randomness (±20%)
        var variation = (decimal)(random.NextDouble() * 0.4 + 0.8); // 0.8 to 1.2
        expectedSalary *= variation;
        
        // Ensure it's within a reasonable range of the job posting
        var minAcceptable = jobPosting.SalaryMin * 0.8m;
        var maxReasonable = jobPosting.SalaryMax * 1.3m;
        
        expectedSalary = Math.Max(minAcceptable, Math.Min(maxReasonable, expectedSalary));
        
        return Math.Round(expectedSalary, 0);
    }
    
    public string GenerateCoverLetter(Human human, Employment employmentProfile, JobPosting jobPosting)
    {
        var templates = new[]
        {
            $"Dear Hiring Manager, I am {human.FirstName} {human.LastName} and I am very interested in the {jobPosting.Title} position. With {employmentProfile.YearsOfExperience} years of experience, I believe I would be a valuable addition to your team.",
            $"Hello, I would like to apply for the {jobPosting.Title} role. My {employmentProfile.YearsOfExperience} years of experience and relevant skills make me excited about this opportunity.",
            $"Dear Sir/Madam, Please consider my application for the {jobPosting.Title} position. I am passionate about this field and have {employmentProfile.YearsOfExperience} years of relevant experience.",
            $"To whom it may concern, I am writing to express my interest in the {jobPosting.Title} role. My background and {employmentProfile.YearsOfExperience} years of experience align well with your requirements.",
            $"Dear Hiring Team, I am excited to apply for the {jobPosting.Title} position. With my skills and {employmentProfile.YearsOfExperience} years of experience, I would welcome the opportunity to contribute to your organization."
        };
        
        return templates[random.Next(templates.Length)];
    }
}