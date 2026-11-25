namespace Genelife.Family;

/// <summary>
/// Tunable relationship rules. Same-sex pairing is disabled for V4a per the
/// confirmed roadmap decision; enabling it is a policy change, not a code path.
/// </summary>
public sealed class RelationshipOptions
{
    public int MinimumPairingAge { get; set; } = 18;
    public int MaximumPairingAge { get; set; } = 70;
    public int MaxAgeGap { get; set; } = 15;
    public bool AllowSameSexPairing { get; set; }
    public decimal SeedPartnershipShare { get; set; } = 0.60m;
    public int PairingThreshold { get; set; } = 5_500;
    public int MaxCandidatesPerGenos { get; set; } = 24;
    public int RejectionCooldownMonths { get; set; } = 6;
    public int NeighbourRadiusTiles { get; set; } = 8;
    public decimal PairingAcceptanceChance { get; set; } = 0.75m;
    public int MaxConsanguinityDepth { get; set; } = 2;
    public bool RejectFirstCousins { get; set; } = true;
    public bool CohabitOnPairing { get; set; } = false;

    public static RelationshipOptions Default { get; } = new();

    public void Validate()
    {
        if (MinimumPairingAge < 0)
            throw new ArgumentOutOfRangeException(nameof(MinimumPairingAge), "MinimumPairingAge must be non-negative.");
        if (MaximumPairingAge < MinimumPairingAge)
            throw new ArgumentOutOfRangeException(nameof(MaximumPairingAge), "MaximumPairingAge must be at least the minimum.");
        if (MaxAgeGap < 0)
            throw new ArgumentOutOfRangeException(nameof(MaxAgeGap), "MaxAgeGap must be non-negative.");
        if (SeedPartnershipShare < 0m || SeedPartnershipShare > 1m)
            throw new ArgumentOutOfRangeException(nameof(SeedPartnershipShare), "SeedPartnershipShare must be between 0 and 1.");
        if (PairingThreshold < 0 || PairingThreshold > 10_000)
            throw new ArgumentOutOfRangeException(nameof(PairingThreshold), "PairingThreshold must be within 0..10000.");
        if (MaxCandidatesPerGenos < 1)
            throw new ArgumentOutOfRangeException(nameof(MaxCandidatesPerGenos), "MaxCandidatesPerGenos must be positive.");
        if (RejectionCooldownMonths < 0)
            throw new ArgumentOutOfRangeException(nameof(RejectionCooldownMonths), "RejectionCooldownMonths must be non-negative.");
        if (NeighbourRadiusTiles < 0)
            throw new ArgumentOutOfRangeException(nameof(NeighbourRadiusTiles), "NeighbourRadiusTiles must be non-negative.");
        if (PairingAcceptanceChance < 0m || PairingAcceptanceChance > 1m)
            throw new ArgumentOutOfRangeException(nameof(PairingAcceptanceChance), "PairingAcceptanceChance must be between 0 and 1.");
        if (MaxConsanguinityDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(MaxConsanguinityDepth), "MaxConsanguinityDepth must be non-negative.");
    }
}
