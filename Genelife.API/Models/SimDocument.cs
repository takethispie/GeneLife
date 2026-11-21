namespace Genelife.Api.Models;

public class SimDocument
{
    public Guid Id { get; set; }
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public float Hunger { get; set; }
    public float Energy { get; set; }
    public float Hygiene { get; set; }
    public float Bladder { get; set; }
    public string CurrentAction { get; set; } = string.Empty;
    public float ActionTimeRemaining { get; set; }
    public bool IsPlayerControlled { get; set; }
    public string? PlayerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}