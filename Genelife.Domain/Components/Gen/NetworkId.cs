namespace Genelife.Components.Gen;

public record struct NetworkId
{
    public ulong Value;

    public NetworkId(ulong value)
    {
        Value = value;
    }
}
