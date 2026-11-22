namespace Genelife.Api.DTOs;

public sealed class WorldDataStatus
{
    public long Tick { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public bool IsRunning { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public List<SimStatus> Sims { get; set; } = new();
    public List<CompanyStatus> Companies { get; set; } = new();
    public List<OfficeStatus> Offices { get; set; } = new();
    public List<EmployeeStatus> Employees { get; set; } = new();
    public List<JobPostingStatus> JobPostings { get; set; } = new();
    public List<JobApplicationStatus> JobApplications { get; set; } = new();
    public List<OpenPositionStatus> OpenPositions { get; set; } = new();
    public EconomyStatus Economy { get; set; } = new();
    public List<LedgerEntryStatus> RecentLedger { get; set; } = new();
}
