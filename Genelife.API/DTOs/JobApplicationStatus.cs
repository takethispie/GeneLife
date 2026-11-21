namespace Genelife.Api.DTOs;

public sealed class JobApplicationStatus
{
    public string Applicant { get; set; } = string.Empty;
    public ulong ApplicantNetworkId { get; set; }
    public string Position { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public float MatchScore { get; set; }
    public DateTime ApplicationDate { get; set; }
}
