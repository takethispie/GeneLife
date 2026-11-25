namespace Genelife.Components.Gen;

public record struct PlayerControlled
{
    public string PlayerId;

    public PlayerControlled(string playerId)
    {
        PlayerId = playerId;
    }
}