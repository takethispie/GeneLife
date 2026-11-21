namespace Genelife.Api.DTOs;

public class SimStatus
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public float Hunger { get; set; }
    public float Energy { get; set; }
    public float Hygiene { get; set; }
    public float Bladder { get; set; }
    public string CurrentAction { get; set; } = string.Empty;
    public float TimeRemaining { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
}