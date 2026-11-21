using Genelife.Api.DTOs;

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

    public SimStatus ToSimStatus()
    {
        return new SimStatus
        {
            NetworkId = NetworkId,
            Name = Name,
            Age = Age,
            IsPlayerControlled = IsPlayerControlled,
            Hunger = MathF.Round(Hunger, 1),
            Energy = MathF.Round(Energy, 1),
            Hygiene = MathF.Round(Hygiene, 1),
            Bladder = MathF.Round(Bladder, 1),
            CurrentAction = CurrentAction,
            TimeRemaining = MathF.Round(ActionTimeRemaining, 2)
        };
    }
}