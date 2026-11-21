namespace Genelife.Web.Models;

public sealed class WorldData
{
    public long Tick { get; set; }
    public bool IsRunning { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public List<SimData> Sims { get; set; } = new();
    public List<CompanyData> Companies { get; set; } = new();
    public List<OfficeData> Offices { get; set; } = new();
    public List<EmployeeData> Employees { get; set; } = new();
    public List<JobPostingData> JobPostings { get; set; } = new();
    public List<JobApplicationData> JobApplications { get; set; } = new();
    public List<OpenPositionData> OpenPositions { get; set; } = new();
}

public sealed class SimData
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsPlayerControlled { get; set; }
    public float Hunger { get; set; }
    public float Energy { get; set; }
    public float Hygiene { get; set; }
    public float Bladder { get; set; }
    public string CurrentAction { get; set; } = string.Empty;
    public float TimeRemaining { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
}

public sealed class CompanyData
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
}

public sealed class OfficeData
{
    public string Name { get; set; } = string.Empty;
    public ulong CompanyNetworkId { get; set; }
    public string Company { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int EmployeeCount { get; set; }
}

public sealed class EmployeeData
{
    public ulong SimNetworkId { get; set; }
    public string Sim { get; set; } = string.Empty;
    public ulong CompanyNetworkId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public float Salary { get; set; }
}

public sealed class JobPostingData
{
    public string Position { get; set; } = string.Empty;
    public ulong CompanyNetworkId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Office { get; set; } = string.Empty;
    public float Salary { get; set; }
    public string Requirements { get; set; } = string.Empty;
    public int OpenPositions { get; set; }
}

public sealed class JobApplicationData
{
    public string Applicant { get; set; } = string.Empty;
    public ulong ApplicantNetworkId { get; set; }
    public string Position { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public float MatchScore { get; set; }
    public DateTime ApplicationDate { get; set; }
}

public sealed class OpenPositionData
{
    public string Position { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Office { get; set; } = string.Empty;
    public float Salary { get; set; }
}
