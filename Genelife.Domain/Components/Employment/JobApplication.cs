using Arch.Core;
using Genelife.Enums;

namespace Genelife.Components.Employment;

/// <summary>
/// Component representing a job application linking an applicant to a job posting
/// </summary>
public record struct JobApplication
{
    public Entity Applicant;
    public Entity JobPosting;
    public Entity Company;
    public string Position;
    public DateTime ApplicationDate;
    public ApplicationStatus Status;
    public float MatchScore;

    public JobApplication(Entity applicant, Entity jobPosting, float matchScore = 0f)
        : this(applicant, jobPosting, string.Empty, Entity.Null, matchScore)
    {
    }

    public JobApplication(Entity applicant, Entity jobPosting, string position, Entity company, float matchScore = 0f)
    {
        Applicant = applicant;
        JobPosting = jobPosting;
        Company = company;
        Position = position;
        ApplicationDate = DateTime.UtcNow;
        Status = ApplicationStatus.Pending;
        MatchScore = matchScore;
    }
}
