using Arch.Core;

namespace Genelife.Components.Family;

public enum FamilyInvitationKind : byte
{
    Partnership = 1,
    Conception = 2,
    Guardianship = 3,
    Cohabitation = 4
}

public enum FamilyInvitationStatus : byte
{
    Pending = 1,
    Accepted = 2,
    Declined = 3,
    Expired = 4,
    Cancelled = 5
}

/// <summary>
/// An expiring consent request between two sims. An invitation is an entity so
/// it can be persisted, audited and projected; acceptance is the only path that
/// creates a relationship edge.
/// </summary>
public record struct FamilyInvitation
{
    public Guid Id;
    public Entity From;
    public Entity To;
    public FamilyInvitationKind Kind;
    public long CreatedTick;
    public long ExpiresTick;
    public FamilyInvitationStatus Status;

    public FamilyInvitation(
        Guid id,
        Entity from,
        Entity to,
        FamilyInvitationKind kind,
        long createdTick,
        long expiresTick)
    {
        Id = id;
        From = from;
        To = to;
        Kind = kind;
        CreatedTick = createdTick;
        ExpiresTick = expiresTick;
        Status = FamilyInvitationStatus.Pending;
    }
}

/// <summary>
/// Per-target consent cooldown. A declined invitation starts a cooldown; repeat
/// requests during it are rejected without a new consent roll.
/// </summary>
public record struct FamilyConsentCooldown
{
    public Entity From;
    public long UntilTick;
}
