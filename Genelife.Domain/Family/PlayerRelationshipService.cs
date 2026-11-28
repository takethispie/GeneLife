using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Player;
using Genelife.Social;

namespace Genelife.Family;

/// <summary>
/// Player-keyed partnership and cohabitation invitations. Invitations survive
/// heir continuation; acceptance resolves the current controlled genoses and
/// revalidates before it touches the V4 relationship index or housing registry.
/// </summary>
public sealed class PlayerRelationshipService
{
    private readonly object gate = new();
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly PlayerRegistry players;
    private readonly RelationshipIndex relationships;
    private readonly RelationshipService relationshipService;
    private readonly SocialGraphService social;
    private readonly RelationshipOptions relationshipOptions;
    private readonly PlayerRelationshipOptions options;
    private readonly HouseholdMergeService households;
    private readonly Dictionary<Guid, PlayerRelationshipInvitation> invitations = new();
    private readonly Dictionary<(string From, string To), long> declineCooldowns = new();

    public PlayerRelationshipService(
        World world,
        EconomyContext economy,
        PlayerRegistry players,
        RelationshipIndex relationships,
        RelationshipService relationshipService,
        SocialGraphService social,
        RelationshipOptions relationshipOptions,
        PlayerRelationshipOptions? options = null)
    {
        this.world = world;
        this.economy = economy;
        this.players = players;
        this.relationships = relationships;
        this.relationshipService = relationshipService;
        this.social = social;
        this.relationshipOptions = relationshipOptions;
        this.options = options ?? new PlayerRelationshipOptions();
        this.options.Validate();
        households = new HouseholdMergeService(world, economy);
    }

    public PlayerRelationshipResult InvitePartner(
        string fromPlayerId,
        string toPlayerId,
        long tick)
    {
        lock (gate)
        {
            RefreshLocked(tick);
            var evaluation = EvaluateInvitePartnerLocked(fromPlayerId, toPlayerId, tick);
            if (!evaluation.Applied)
                return evaluation;

            if (!TryResolve(fromPlayerId, out var fromGenos) || !TryResolve(toPlayerId, out var toGenos))
                return PlayerRelationshipResult.Reject("not_available", "Both players need an active genos.");

            return CreateInvitationLocked(
                fromPlayerId,
                toPlayerId,
                fromGenos,
                toGenos,
                FamilyInvitationKind.Partnership,
                tick);
        }
    }

    /// <summary>
    /// Pure precondition pass for a partnership invitation, shared by the applying path and the
    /// action catalogue (GDD chapter 08 §8.5(1)): no world mutation and no invitation created.
    /// </summary>
    public PlayerRelationshipResult EvaluateInvitePartner(string fromPlayerId, string toPlayerId, long tick)
    {
        lock (gate)
            return EvaluateInvitePartnerLocked(fromPlayerId, toPlayerId, tick);
    }

    private PlayerRelationshipResult EvaluateInvitePartnerLocked(
        string fromPlayerId,
        string toPlayerId,
        long tick)
    {
        if (Same(fromPlayerId, toPlayerId))
            return PlayerRelationshipResult.Reject("self_target", "You cannot invite yourself.");
        if (social.AreInvitationsBlocked(fromPlayerId, tick))
            return PlayerRelationshipResult.Reject("suspended", "Your invitations are currently restricted.");
        if (social.IsBlocked(fromPlayerId, toPlayerId))
            return PlayerRelationshipResult.Reject("blocked", "You cannot invite that player.");
        if (!TryResolve(fromPlayerId, out var fromGenos) || !TryResolve(toPlayerId, out var toGenos))
            return PlayerRelationshipResult.Reject("not_available", "Both players need an active genos.");
        if (IsOnCooldown(fromPlayerId, toPlayerId, tick))
            return PlayerRelationshipResult.Reject("cooldown", "That invitation was declined recently.");
        if (HasPendingLocked(fromPlayerId, toPlayerId, FamilyInvitationKind.Partnership)
            || HasAnyPendingFromLocked(fromPlayerId, FamilyInvitationKind.Partnership))
            return PlayerRelationshipResult.Reject("request_pending", "An invitation is already pending.");

        var eligibility = EligibilityPolicy.Evaluate(
            world,
            relationships,
            fromGenos,
            toGenos,
            relationshipOptions);
        if (!eligibility.Eligible)
            return PlayerRelationshipResult.Reject(
                eligibility.Code ?? "invalid_state",
                eligibility.Reason ?? "The pair is not eligible.");

        return PlayerRelationshipResult.Success();
    }

    public PlayerRelationshipResult InviteCohabit(
        string fromPlayerId,
        string toPlayerId,
        long tick)
    {
        lock (gate)
        {
            RefreshLocked(tick);
            var evaluation = EvaluateInviteCohabitLocked(fromPlayerId, toPlayerId, tick);
            if (!evaluation.Applied)
                return evaluation;

            if (!TryResolve(fromPlayerId, out var fromGenos) || !TryResolve(toPlayerId, out var toGenos))
                return PlayerRelationshipResult.Reject("not_available", "Both players need an active genos.");

            return CreateInvitationLocked(
                fromPlayerId,
                toPlayerId,
                fromGenos,
                toGenos,
                FamilyInvitationKind.Cohabitation,
                tick);
        }
    }

    /// <summary>Pure precondition pass for a cohabitation invitation; shared by apply and catalogue.</summary>
    public PlayerRelationshipResult EvaluateInviteCohabit(string fromPlayerId, string toPlayerId, long tick)
    {
        lock (gate)
            return EvaluateInviteCohabitLocked(fromPlayerId, toPlayerId, tick);
    }

    private PlayerRelationshipResult EvaluateInviteCohabitLocked(
        string fromPlayerId,
        string toPlayerId,
        long tick)
    {
        if (Same(fromPlayerId, toPlayerId))
            return PlayerRelationshipResult.Reject("self_target", "You cannot invite yourself.");
        if (social.AreInvitationsBlocked(fromPlayerId, tick))
            return PlayerRelationshipResult.Reject("suspended", "Your invitations are currently restricted.");
        if (social.IsBlocked(fromPlayerId, toPlayerId))
            return PlayerRelationshipResult.Reject("blocked", "You cannot invite that player.");
        if (!TryResolve(fromPlayerId, out var fromGenos) || !TryResolve(toPlayerId, out var toGenos))
            return PlayerRelationshipResult.Reject("not_available", "Both players need an active genos.");
        if (!relationships.PartnersOf(fromGenos).Contains(toGenos)
            || !relationships.PartnersOf(toGenos).Contains(fromGenos))
            return PlayerRelationshipResult.Reject("not_partner", "You need an active partnership first.");
        if (IsOnCooldown(fromPlayerId, toPlayerId, tick))
            return PlayerRelationshipResult.Reject("cooldown", "That invitation was declined recently.");
        if (HasPendingLocked(fromPlayerId, toPlayerId, FamilyInvitationKind.Cohabitation)
            || HasAnyPendingFromLocked(fromPlayerId, FamilyInvitationKind.Cohabitation))
            return PlayerRelationshipResult.Reject("request_pending", "A cohabitation invitation is already pending.");
        if (SameHousehold(fromGenos, toGenos))
            return PlayerRelationshipResult.Reject("already_cohoused", "You already share a household.");

        return PlayerRelationshipResult.Success();
    }

    /// <summary>Pure precondition pass for ending a partnership; shared by apply and catalogue.</summary>
    public PlayerRelationshipResult EvaluateEndPartnership(string playerId, string partnerPlayerId)
    {
        lock (gate)
            return EvaluateEndPartnershipLocked(playerId, partnerPlayerId);
    }

    private PlayerRelationshipResult EvaluateEndPartnershipLocked(string playerId, string partnerPlayerId)
    {
        if (Same(playerId, partnerPlayerId))
            return PlayerRelationshipResult.Reject("invalid_state", "You cannot end a partnership with yourself.");
        if (!TryResolve(playerId, out var genos) || !TryResolve(partnerPlayerId, out var partner))
            return PlayerRelationshipResult.Reject("not_available", "Both players need an active genos.");
        if (!relationships.PartnersOf(genos).Contains(partner)
            || !relationships.PartnersOf(partner).Contains(genos))
            return PlayerRelationshipResult.Reject("not_partner", "No active partnership exists.");

        return PlayerRelationshipResult.Success();
    }

    public PlayerRelationshipResult Respond(
        string actorPlayerId,
        Guid invitationId,
        bool accept,
        long tick)
    {
        lock (gate)
        {
            RefreshLocked(tick);
            if (!invitations.TryGetValue(invitationId, out var invitation))
                return PlayerRelationshipResult.Reject("offer_not_found", "That invitation no longer exists.");
            if (!Same(invitation.ToPlayerId, actorPlayerId))
                return PlayerRelationshipResult.Reject("not_target", "That invitation was not sent to you.");
            if (invitation.Status == FamilyInvitationStatus.Expired)
                return PlayerRelationshipResult.Reject("offer_expired", "That invitation has expired.");
            if (invitation.Status != FamilyInvitationStatus.Pending)
                return PlayerRelationshipResult.Reject("invalid_state", "That invitation is no longer pending.");
            if (tick > invitation.ExpiresTick)
            {
                SetStatusLocked(invitation, FamilyInvitationStatus.Expired);
                EmitResponded(invitation, accepted: false, code: "expired", tick);
                return PlayerRelationshipResult.Reject("offer_expired", "That invitation has expired.");
            }

            if (!accept)
            {
                SetStatusLocked(invitation, FamilyInvitationStatus.Declined);
                declineCooldowns[(invitation.FromPlayerId, invitation.ToPlayerId)] =
                    tick + options.DeclineCooldownTicks;
                EmitResponded(invitation, accepted: false, code: "declined", tick);
                return PlayerRelationshipResult.Success("Invitation declined.", invitation);
            }

            if (!TryResolve(invitation.FromPlayerId, out var fromGenos)
                || !TryResolve(invitation.ToPlayerId, out var toGenos))
            {
                SetStatusLocked(invitation, FamilyInvitationStatus.Expired);
                EmitResponded(invitation, accepted: false, code: "not_available", tick);
                return PlayerRelationshipResult.Reject("not_available", "One of the players has no active genos.");
            }

            if (social.AreInvitationsBlocked(actorPlayerId, tick))
            {
                EmitResponded(invitation, accepted: false, code: "suspended", tick);
                return PlayerRelationshipResult.Reject("suspended", "Your invitations are currently restricted.");
            }

            if (social.IsBlocked(invitation.FromPlayerId, invitation.ToPlayerId))
            {
                SetStatusLocked(invitation, FamilyInvitationStatus.Cancelled);
                EmitResponded(invitation, accepted: false, code: "blocked", tick);
                return PlayerRelationshipResult.Reject("blocked", "The invitation is blocked.");
            }

            return invitation.Kind switch
            {
                FamilyInvitationKind.Partnership => AcceptPartnershipLocked(invitation, fromGenos, toGenos, tick),
                FamilyInvitationKind.Cohabitation => AcceptCohabitationLocked(invitation, fromGenos, toGenos, tick),
                _ => PlayerRelationshipResult.Reject("not_available", "That invitation kind is not supported.")
            };
        }
    }

    public PlayerRelationshipResult EndPartnership(
        string playerId,
        string partnerPlayerId,
        long tick,
        bool splitHousehold = true)
    {
        lock (gate)
        {
            RefreshLocked(tick);
            var evaluation = EvaluateEndPartnershipLocked(playerId, partnerPlayerId);
            if (!evaluation.Applied)
                return evaluation;

            if (!TryResolve(playerId, out var genos) || !TryResolve(partnerPlayerId, out var partner))
                return PlayerRelationshipResult.Reject("not_available", "Both players need an active genos.");

            var firstId = NetworkIdOf(genos);
            var secondId = NetworkIdOf(partner);
            if (!relationshipService.EndPartnership(genos, partner, tick))
                return PlayerRelationshipResult.Reject("invalid_state", "The partnership could not be ended.");

            economy.Emit(new PartnershipEnded(firstId, secondId, "player-ended", tick));

            if (splitHousehold)
                SplitHouseholdLocked(genos, partner, tick);

            return PlayerRelationshipResult.Success("Partnership ended.");
        }
    }

    public PlayerRelationshipStatus BuildStatus(string playerId, long tick)
    {
        lock (gate)
        {
            RefreshLocked(tick);
            if (!TryResolve(playerId, out var genos))
                return new PlayerRelationshipStatus(
                    playerId, null, null, null, false, false, Array.Empty<PlayerRelationshipInvitation>());

            var partner = relationships.PartnersOf(genos).FirstOrDefault(candidate =>
                world.IsAlive(candidate) && !world.Has<Dead>(candidate));
            var partnerPlayerId = partner != Entity.Null && players.TryGetByGenos(partner, out var partnerProfile)
                ? partnerProfile.PlayerId
                : null;
            var partnerName = partner != Entity.Null && world.Has<GenName>(partner)
                ? world.Get<GenName>(partner).Name
                : null;
            var sharesHousehold = partner != Entity.Null && SameHousehold(genos, partner);
            var pending = invitations.Values
                .Where(invitation => Same(invitation.FromPlayerId, playerId)
                    || Same(invitation.ToPlayerId, playerId))
                .OrderByDescending(invitation => invitation.CreatedTick)
                .Take(20)
                .ToArray();

            return new PlayerRelationshipStatus(
                playerId,
                partnerPlayerId,
                partner != Entity.Null ? NetworkIdOf(partner) : null,
                partnerName,
                sharesHousehold,
                partner != Entity.Null && !sharesHousehold,
                pending);
        }
    }

    public IReadOnlyList<PlayerRelationshipInvitation> ExportInvitations()
    {
        lock (gate)
            return invitations.Values
                .OrderBy(invitation => invitation.CreatedTick)
                .ToArray();
    }

    public void RestoreInvitations(IEnumerable<PlayerRelationshipInvitation> restored)
    {
        lock (gate)
        {
            invitations.Clear();
            declineCooldowns.Clear();
            foreach (var invitation in restored)
                invitations[invitation.Id] = invitation;
        }
    }

    public IReadOnlyList<PlayerRelationshipInvitation> PendingInvitations(string playerId, long tick)
    {
        lock (gate)
        {
            RefreshLocked(tick);
            return invitations.Values
                .Where(invitation => invitation.Status == FamilyInvitationStatus.Pending
                    && (Same(invitation.FromPlayerId, playerId) || Same(invitation.ToPlayerId, playerId)))
                .OrderBy(invitation => invitation.CreatedTick)
                .ToArray();
        }
    }

    public PlayerRelationshipInvitation? FindInvitation(Guid id, long tick)
    {
        lock (gate)
        {
            RefreshLocked(tick);
            return invitations.TryGetValue(id, out var invitation) ? invitation : null;
        }
    }

    private PlayerRelationshipResult CreateInvitationLocked(
        string fromPlayerId,
        string toPlayerId,
        Entity fromGenos,
        Entity toGenos,
        FamilyInvitationKind kind,
        long tick)
    {
        var invitation = new PlayerRelationshipInvitation
        {
            Id = Guid.NewGuid(),
            FromPlayerId = fromPlayerId,
            ToPlayerId = toPlayerId,
            Kind = kind,
            Status = FamilyInvitationStatus.Pending,
            CreatedTick = tick,
            ExpiresTick = tick + options.InvitationLifetimeTicks
        };
        invitations[invitation.Id] = invitation;
        economy.Emit(new PlayerInvited(
            NetworkIdOf(fromGenos),
            NetworkIdOf(toGenos),
            invitation.Id,
            kind.ToString(),
            invitation.ExpiresTick,
            tick));
        return PlayerRelationshipResult.Success("Invitation sent.", invitation, new { pendingConsent = true });
    }

    private PlayerRelationshipResult AcceptPartnershipLocked(
        PlayerRelationshipInvitation invitation,
        Entity fromGenos,
        Entity toGenos,
        long tick)
    {
        var eligibility = EligibilityPolicy.Evaluate(
            world,
            relationships,
            fromGenos,
            toGenos,
            relationshipOptions);
        if (!eligibility.Eligible)
        {
            SetStatusLocked(invitation, FamilyInvitationStatus.Expired);
            EmitResponded(invitation, accepted: false, code: eligibility.Code, tick);
            return PlayerRelationshipResult.Reject(
                eligibility.Code ?? "invalid_state",
                eligibility.Reason ?? "The pair is not eligible.");
        }

        if (!relationshipService.TryCreatePair(
                fromGenos,
                toGenos,
                RelationshipKind.Partner,
                RelationshipSource.Player,
                tick,
                out _,
                out _,
                out var error))
        {
            SetStatusLocked(invitation, FamilyInvitationStatus.Expired);
            EmitResponded(invitation, accepted: false, code: error.ToString(), tick);
            return PlayerRelationshipResult.Reject("invalid_state", "The partnership could not be formed.");
        }

        economy.Emit(new PartnershipFormed(NetworkIdOf(fromGenos), NetworkIdOf(toGenos), 0, tick));
        SetStatusLocked(invitation, FamilyInvitationStatus.Accepted);
        EmitResponded(invitation, accepted: true, code: null, tick);
        return PlayerRelationshipResult.Success("Invitation accepted.", invitation, new
        {
            partnerGenosNetworkId = NetworkIdOf(toGenos)
        });
    }

    private PlayerRelationshipResult AcceptCohabitationLocked(
        PlayerRelationshipInvitation invitation,
        Entity fromGenos,
        Entity toGenos,
        long tick)
    {
        if (!relationships.PartnersOf(fromGenos).Contains(toGenos)
            || !relationships.PartnersOf(toGenos).Contains(fromGenos))
        {
            SetStatusLocked(invitation, FamilyInvitationStatus.Cancelled);
            EmitResponded(invitation, accepted: false, code: "not_partner", tick);
            return PlayerRelationshipResult.Reject("not_partner", "The partnership is no longer active.");
        }

        if (SameHousehold(fromGenos, toGenos))
        {
            SetStatusLocked(invitation, FamilyInvitationStatus.Accepted);
            EmitResponded(invitation, accepted: true, code: null, tick);
            return PlayerRelationshipResult.Success("Invitation accepted.", invitation, new
            {
                merged = false
            });
        }

        if (!households.TryMergePartners(
                fromGenos,
                toGenos,
                out _,
                out _,
                out var fromHouseholdId,
                out var toHouseholdId))
        {
            EmitResponded(invitation, accepted: false, code: "merge_unavailable", tick);
            return PlayerRelationshipResult.Reject(
                "merge_unavailable",
                "The households cannot be merged right now. The partnership remains.");
        }

        if (fromHouseholdId != 0 && toHouseholdId != 0 && fromHouseholdId != toHouseholdId)
            economy.Emit(new HouseholdMerged(fromHouseholdId, toHouseholdId, tick));

        SetStatusLocked(invitation, FamilyInvitationStatus.Accepted);
        EmitResponded(invitation, accepted: true, code: null, tick);
        return PlayerRelationshipResult.Success("Invitation accepted.", invitation, new
        {
            merged = true,
            fromHouseholdId,
            toHouseholdId
        });
    }

    private void SplitHouseholdLocked(Entity first, Entity second, long tick)
    {
        var household = economy.Housing.HouseholdOf(first);
        if (household == Entity.Null || household != economy.Housing.HouseholdOf(second))
            return;

        var head = world.Has<Components.Housing.Household>(household)
            ? world.Get<Components.Housing.Household>(household).Head
            : Entity.Null;
        var leaver = head == first ? second : first;
        if (!households.TrySplit(leaver, tick, out var newHousehold) || newHousehold == Entity.Null)
            return;

        economy.Emit(new HouseholdSplit(
            NetworkIdOf(newHousehold),
            NetworkIdOf(household),
            tick));
    }

    private void RefreshLocked(long tick)
    {
        foreach (var invitation in invitations.Values.ToArray())
        {
            if (invitation.Status != FamilyInvitationStatus.Pending)
                continue;

            if (invitation.ExpiresTick < tick)
            {
                invitations[invitation.Id] = invitation with { Status = FamilyInvitationStatus.Expired };
                continue;
            }

            if (social.IsBlocked(invitation.FromPlayerId, invitation.ToPlayerId))
                invitations[invitation.Id] = invitation with { Status = FamilyInvitationStatus.Cancelled };
        }
    }

    private void SetStatusLocked(PlayerRelationshipInvitation invitation, FamilyInvitationStatus status) =>
        invitations[invitation.Id] = invitation with { Status = status };

    private bool HasPendingLocked(string fromPlayerId, string toPlayerId, FamilyInvitationKind kind) =>
        invitations.Values.Any(invitation =>
            invitation.Status == FamilyInvitationStatus.Pending
            && invitation.Kind == kind
            && ((Same(invitation.FromPlayerId, fromPlayerId) && Same(invitation.ToPlayerId, toPlayerId))
                || (Same(invitation.FromPlayerId, toPlayerId) && Same(invitation.ToPlayerId, fromPlayerId))));

    private bool HasAnyPendingFromLocked(string fromPlayerId, FamilyInvitationKind kind) =>
        invitations.Values.Any(invitation =>
            invitation.Status == FamilyInvitationStatus.Pending
            && invitation.Kind == kind
            && Same(invitation.FromPlayerId, fromPlayerId));

    private bool IsOnCooldown(string fromPlayerId, string toPlayerId, long tick) =>
        declineCooldowns.TryGetValue((fromPlayerId, toPlayerId), out var until) && tick < until;

    private bool TryResolve(string playerId, out Entity genos)
    {
        genos = Entity.Null;
        if (!players.TryGet(playerId, out var profile)
            || profile.ControlledGenos == Entity.Null
            || !world.IsAlive(profile.ControlledGenos)
            || world.Has<Dead>(profile.ControlledGenos))
            return false;

        genos = profile.ControlledGenos;
        return true;
    }

    private bool SameHousehold(Entity first, Entity second)
    {
        var firstHousehold = economy.Housing.HouseholdOf(first);
        return firstHousehold != Entity.Null && firstHousehold == economy.Housing.HouseholdOf(second);
    }

    private void EmitResponded(
        PlayerRelationshipInvitation invitation,
        bool accepted,
        string? code,
        long tick)
    {
        var fromId = TryResolve(invitation.FromPlayerId, out var fromGenos) ? NetworkIdOf(fromGenos) : 0;
        var toId = TryResolve(invitation.ToPlayerId, out var toGenos) ? NetworkIdOf(toGenos) : 0;
        economy.Emit(new PlayerInvitationResponded(
            fromId,
            toId,
            invitation.Id,
            invitation.Kind.ToString(),
            accepted,
            code,
            tick));
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<Components.Gen.NetworkId>(entity) ? world.Get<Components.Gen.NetworkId>(entity).Value : (ulong)entity.Id;

    private static bool Same(string first, string second) =>
        string.Equals(first, second, StringComparison.Ordinal);
}
