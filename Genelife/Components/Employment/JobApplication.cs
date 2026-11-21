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
    public DateTime ApplicationDate;
    public ApplicationStatus Status;
    public float MatchScore;

    public JobApplication(Entity applicant, Entity jobPosting, float matchScore = 0f)
    {
        Applicant = applicant;
        JobPosting = jobPosting;
        ApplicationDate = DateTime.UtcNow;
        Status = ApplicationStatus.Pending;
        MatchScore = matchScore;
    }
}
