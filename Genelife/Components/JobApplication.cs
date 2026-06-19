using Genelife.Enums;

namespace Genelife.Components;

/// <summary>
/// Component representing a job application linking an applicant to a job posting
/// </summary>
public record struct JobApplication
{
    public int ApplicantEntityId;
    public int JobPostingEntityId;
    public DateTime ApplicationDate;
    public ApplicationStatus Status;
    public float MatchScore;
    
    public JobApplication(int applicantEntityId, int jobPostingEntityId, float matchScore = 0f)
    {
        ApplicantEntityId = applicantEntityId;
        JobPostingEntityId = jobPostingEntityId;
        ApplicationDate = DateTime.UtcNow;
        Status = ApplicationStatus.Pending;
        MatchScore = matchScore;
    }
}
