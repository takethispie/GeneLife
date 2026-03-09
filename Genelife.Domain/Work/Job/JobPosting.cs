using Genelife.Domain.Work.Skills;

namespace Genelife.Domain.Work.Job;

public class JobPosting(
    Guid companyId,
    string title,
    float salaryMin,
    float salaryMax,
    CompanyType companyType,
    JobLevel level,
    SkillSet requiredSkillSet,
    Position officeLocation,
    int maxApplications = 50
)
{
    public Guid CompanyId { get; init; } = companyId;
    public string Title { get; init; } = title;
    public float SalaryMin { get; init; } = salaryMin;
    public float SalaryMax { get; init; } = salaryMax;
    public CompanyType CompanyType { get; init; } = companyType;
    public JobLevel Level { get; init; } = level;
    public SkillSet RequiredSkillSet { get; init; } = requiredSkillSet;
    public Position OfficeLocation { get; init; } = officeLocation;
    public int MaxApplications { get; init; } = maxApplications;

    public float CalculateSalaryOffer(JobApplication application)
    {
        var requestedSalary = application.RequestedSalary;
        var salaryMin = SalaryMin;
        var salaryMax = SalaryMax;

        // If requested salary is within range, offer it
        if (requestedSalary >= salaryMin && requestedSalary <= salaryMax)
            return requestedSalary;

        // If requested is below minimum, offer minimum
        if (requestedSalary < salaryMin)
            return salaryMin;

        // If requested is above maximum, negotiate based on match score
        var maxOffer = application.MatchScore >= 0.8f ? salaryMax * 1.1f : salaryMax;
        return Math.Min(requestedSalary, maxOffer);
    }
}