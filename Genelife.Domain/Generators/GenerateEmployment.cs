using Genelife.Domain.Work;
using Genelife.Domain.Work.Skills;

namespace Genelife.Domain.Generators;

public class GenerateEmployment
{
    private readonly Random random = new();
    
    public (SkillSet, int) Execute(Human human)
    {
        // Generate years of experience based on age
        var age = DateTime.Now.Year - human.Birthday.Year;
        var maxExperience = Math.Max(0, age - 18); // Assume work starts at 18
        var experience = random.Next(0, Math.Min(maxExperience + 1, 25)); // Cap at 25 years

        return (GenerateSkills(experience), experience);
    }
    
    private SkillSet GenerateSkills(int experience)
    {
        var skillSet = new SkillSet();
        
        // Always add some common skills
        var commonSkills = SkillExtensions.GetAllValues<CommonSkill>();
        var numCommonSkills = Math.Min(random.Next(3, 7), commonSkills.Length);
        var selectedCommonSkills = commonSkills
            .OrderBy(x => random.Next())
            .Take(numCommonSkills)
            .ToList();
        skillSet.CommonSkills.AddRange(selectedCommonSkills);
        
        var allSpecializations = new List<string>
        {
            "Technical",
            "Business",
            "Healthcare",
            "Manufacturing",
            "Retail"
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
            
            switch (specialization)
            {
                case "Technical":
                    var technicalSkills = SkillExtensions.GetAllValues<TechnicalSkill>();
                    var selectedTechnicalSkills = technicalSkills
                        .OrderBy(x => random.Next())
                        .Take(Math.Min(numSpecializedSkills, technicalSkills.Length))
                        .ToList();
                    skillSet.TechnicalSkills.AddRange(selectedTechnicalSkills);
                    break;
                    
                case "Business":
                    var businessSkills = SkillExtensions.GetAllValues<BusinessSkill>();
                    var selectedBusinessSkills = businessSkills
                        .OrderBy(x => random.Next())
                        .Take(Math.Min(numSpecializedSkills, businessSkills.Length))
                        .ToList();
                    skillSet.BusinessSkills.AddRange(selectedBusinessSkills);
                    break;
                    
                case "Healthcare":
                    var healthcareSkills = SkillExtensions.GetAllValues<HealthcareSkill>();
                    var selectedHealthcareSkills = healthcareSkills
                        .OrderBy(x => random.Next())
                        .Take(Math.Min(numSpecializedSkills, healthcareSkills.Length))
                        .ToList();
                    skillSet.HealthcareSkills.AddRange(selectedHealthcareSkills);
                    break;
                    
                case "Manufacturing":
                    var manufacturingSkills = SkillExtensions.GetAllValues<ManufacturingSkill>();
                    var selectedManufacturingSkills = manufacturingSkills
                        .OrderBy(x => random.Next())
                        .Take(Math.Min(numSpecializedSkills, manufacturingSkills.Length))
                        .ToList();
                    skillSet.ManufacturingSkills.AddRange(selectedManufacturingSkills);
                    break;
                    
                case "Retail":
                    var retailSkills = SkillExtensions.GetAllValues<RetailSkill>();
                    var selectedRetailSkills = retailSkills
                        .OrderBy(x => random.Next())
                        .Take(Math.Min(numSpecializedSkills, retailSkills.Length))
                        .ToList();
                    skillSet.RetailSkills.AddRange(selectedRetailSkills);
                    break;
            }
        }
        
        return skillSet;
    }
    
    public float GenerateDesiredSalary(int yearsOfExperience, JobPosting jobPosting)
    {
        // Base salary expectation on experience and job level
        var experienceMultiplier = 1.0f + (yearsOfExperience * 0.05); // 5% per year of experience
        
        var baseSalary = jobPosting.Level switch
        {
            JobLevel.Entry => 30000,
            JobLevel.Junior => 40000,
            JobLevel.Mid => 55000,
            JobLevel.Senior => 75000,
            JobLevel.Lead => 90000,
            JobLevel.Manager => 100000,
            JobLevel.Director => 130000,
            JobLevel.Executive => 180000,
            _ => 40000
        };
        
        var expectedSalary = baseSalary * experienceMultiplier;
        
        // Add some randomness (±20%)
        var variation = random.NextSingle() * 0.4 + 0.8; // 0.8 to 1.2
        expectedSalary *= variation;
        
        // Ensure it's within a reasonable range of the job posting
        var minAcceptable = jobPosting.SalaryMin * 0.8f;
        var maxReasonable = jobPosting.SalaryMax * 1.3f;
        
        expectedSalary = Math.Max(minAcceptable, Math.Min(maxReasonable, expectedSalary));
        
        return Convert.ToSingle(Math.Round(expectedSalary, 0));
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