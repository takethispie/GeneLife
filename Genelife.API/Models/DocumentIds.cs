namespace Genelife.Api.Models;

public static class DocumentIds
{
    /// <summary>
    /// Deterministic document identity for a network id so repeated saves upsert the
    /// same Marten document instead of appending duplicates.
    /// </summary>
    public static Guid ForNetworkId(ulong networkId)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, networkId);
        return new Guid(bytes);
    }
}
