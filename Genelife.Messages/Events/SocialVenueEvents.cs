namespace Genelife.Messages.Events;

public sealed record VenueEntered(
    ulong VenueNetworkId,
    ulong SimNetworkId,
    long Tick);

public sealed record VenueLeft(
    ulong VenueNetworkId,
    ulong SimNetworkId,
    long Tick);

public sealed record VenueEntryRejected(
    ulong VenueNetworkId,
    string PlayerId,
    string Code,
    long Tick);

public sealed record VenueClosed(
    ulong VenueNetworkId,
    bool Closed,
    long Tick);

public sealed record VenueOccupantRemoved(
    ulong VenueNetworkId,
    string PlayerId,
    long Tick);

public sealed record VenueActivityPerformed(
    ulong VenueNetworkId,
    ulong SimNetworkId,
    string Activity,
    long Tick);
