namespace Genelife.Api.DTOs;

public sealed class OpenPositionStatus
{
    public string Position { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Office { get; set; } = string.Empty;
    public float Salary { get; set; }
}
