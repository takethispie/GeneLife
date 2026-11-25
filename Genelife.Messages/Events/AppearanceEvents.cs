namespace Genelife.Messages.Events;

public sealed record AppearanceDerived(
    ulong GenosNetworkId,
    byte AppearanceVersion,
    uint DescriptorHash,
    long Tick);

public sealed record AppearanceMigrated(
    ulong GenosNetworkId,
    byte AppearanceVersion,
    int SkinTone,
    int FaceShape,
    long Tick);

public sealed record AppearanceVersionChanged(
    ulong GenosNetworkId,
    byte FromVersion,
    byte ToVersion,
    long Tick);
