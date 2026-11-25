using Arch.Core;
using Genelife.Values;

namespace Genelife.Components.Finance;

/// <summary>
/// A minor's inheritance held outside the guardian's wallet. One trust entity
/// per depositing estate so a child can receive separate trusts.
/// </summary>
public record struct TrustAccount
{
    public Money Balance;
    public Entity Beneficiary;
    public Entity Guardian;
    public long CreatedTick;
    public long ReleaseAgeTicks;
    public ulong SourceSimNetworkId;
}
