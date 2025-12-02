using Genelife.Work.Messages.DTOs;
using Genelife.Work.Messages.DTOs.Skills;

namespace Genelife.Work.Usecases;

public class CalculateMatchScore
{
    public float Execute(JobPosting jobPosting, JobApplication application)
    {
        float score = 0.0f;
        
        // Experience level matching (30% weight)
        var experienceScore = CalculateExperienceScore(jobPosting.Level, application.YearsOfExperience);
        score += experienceScore * 0.3f;
        
        // Skills matching (40% weight)
        var skillsScore = CalculateSkillsScore(jobPosting.RequiredSkillSet, application.Skills);
        score += skillsScore * 0.4f;
        
        // Salary expectations (20% weight)
        var salaryScore = CalculateSalaryScore(jobPosting.SalaryMin, jobPosting.SalaryMax, application.RequestedSalary);
        score += salaryScore * 0.2f;
        
        // Industry preference (10% weight)
        // Assume good fit for now
        var industryScore = 0.8f; 
        score += industryScore * 0.1f;
        
        return Convert.ToSingle(Math.Min(1.0, Math.Max(0.0f, score)));
    }
    
    private float CalculateExperienceScore(JobLevel requiredLevel, int yearsOfExperience)
    {
        var requiredYears = requiredLevel switch
        {
            JobLevel.Entry => 0,
            JobLevel.Junior => 2,
            JobLevel.Mid => 5,
            JobLevel.Senior => 8,
            JobLevel.Lead => 10,
            JobLevel.Manager => 12,
            JobLevel.Director => 15,
            JobLevel.Executive => 20,
            _ => 5
        };
        
        if (yearsOfExperience >= requiredYears)
        {
            var bonus = Math.Min(0.2f, (yearsOfExperience - requiredYears) * 0.02f);
            return 1.0f + Convert.ToSingle(bonus);
        }
        
        var ratio = yearsOfExperience / Math.Max(1, requiredYears);
        return ratio * 0.8f; 
    }
    
    private float CalculateSkillsScore(SkillSet requiredSkills, SkillSet applicantSkills)
    {
        if (requiredSkills.Count == 0) return 1.0f;
        return applicantSkills.HasMinimumRequirements(requiredSkills) ?  1.0f : 0.0f;
    }
    
    private float CalculateSalaryScore(float salaryMin, float salaryMax, float requestedSalary)
    {
        if (requestedSalary >= salaryMin && requestedSalary <= salaryMax)
            // Perfect match
            return 1.0f; 
        
        if (requestedSalary < salaryMin)
            // Candidate wants less - good for company
            return 1.0f;
        
        // Candidate wants more than max
        var overage = requestedSalary - salaryMax;
        // 20% over budget is still acceptable
        var maxOverage = salaryMax * 0.2f; 
        
        if (overage <= maxOverage)
            // Linear penalty up to 30%
            return 1.0f - overage / maxOverage * 0.3f; 
        // Significant penalty for high salary expectations
        return 0.4f; 
    }
}