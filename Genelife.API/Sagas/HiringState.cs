namespace Genelife.Api.Sagas;

public enum HiringStatus
{
    Active,
    AllSeatsFilled,
    TimedOut,
    Cancelled
}

public class HiringState
{
    public Guid Id { get; set; }
    public ulong CompanyId { get; set; }
    public ulong PostingId { get; set; }
    public string Position { get; set; } = string.Empty;
    public float Salary { get; set; }
    public int OpenSeats { get; set; }
    public List<ulong> ApplicantIds { get; set; } = new();
    public List<ulong> FilledSeatSimIds { get; set; } = new();
    public HiringStatus Status { get; set; } = HiringStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}