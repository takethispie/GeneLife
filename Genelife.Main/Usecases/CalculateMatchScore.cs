using Genelife.Domain;

namespace Genelife.Main.Usecases;

public class CalculateMatchScore
{
    public decimal Execute(JobPosting jobPosting, JobApplication application)
    {
        decimal score = 0.0m;
        
        // Experience level matching (30% weight)
        var experienceScore = CalculateExperienceScore(jobPosting.Level, application.YearsOfExperience);
        score += experienceScore * 0.3m;
        
        // Skills matching (40% weight)
        var skillsScore = CalculateSkillsScore(jobPosting.Requirements, application.Skills);
        score += skillsScore * 0.4m;
        
        // Salary expectations (20% weight)
        var salaryScore = CalculateSalaryScore(jobPosting.SalaryMin, jobPosting.SalaryMax, application.RequestedSalary);
        score += salaryScore * 0.2m;
        
        // Industry preference (10% weight)
        var industryScore = 0.8m; // Assume good fit for now
        score += industryScore * 0.1m;
        
        return Math.Min(1.0m, Math.Max(0.0m, score));
    }
    
    private decimal CalculateExperienceScore(JobLevel requiredLevel, int yearsOfExperience)
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
            // Bonus for more experience, but diminishing returns
            var bonus = Math.Min(0.2m, (yearsOfExperience - requiredYears) * 0.02m);
            return 1.0m + bonus;
        }
        
        // Penalty for less experience
        var ratio = (decimal)yearsOfExperience / Math.Max(1, requiredYears);
        return ratio * 0.8m; // Max 80% if no experience
    }
    
    private decimal CalculateSkillsScore(List<string> requiredSkills, List<string> applicantSkills)
    {
        if (requiredSkills.Count == 0) return 1.0m;
        
        var matchedSkills = requiredSkills.Intersect(applicantSkills, StringComparer.OrdinalIgnoreCase).Count();
        var baseScore = (decimal)matchedSkills / requiredSkills.Count;
        
        // Bonus for additional skills
        var extraSkills = applicantSkills.Except(requiredSkills, StringComparer.OrdinalIgnoreCase).Count();
        var bonus = Math.Min(0.2m, extraSkills * 0.05m);
        
        return Math.Min(1.0m, baseScore + bonus);
    }
    
    private decimal CalculateSalaryScore(decimal salaryMin, decimal salaryMax, decimal requestedSalary)
    {
        if (requestedSalary >= salaryMin && requestedSalary <= salaryMax)
            return 1.0m; // Perfect match
        
        if (requestedSalary < salaryMin)
            // Candidate wants less - good for company
            return 1.0m;
        
        // Candidate wants more than max
        var overage = requestedSalary - salaryMax;
        var maxOverage = salaryMax * 0.2m; // 20% over budget is still acceptable
        
        if (overage <= maxOverage)
            return 1.0m - overage / maxOverage * 0.3m; // Linear penalty up to 30%
        
        return 0.4m; // Significant penalty for high salary expectations
    }
}