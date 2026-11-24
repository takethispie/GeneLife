using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Components.Player;
using Genelife.Components.Social;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Player;
using Genelife.Systems;

namespace Genelife.Social;

public sealed record VenueActivityNotice(
    string PlayerId,
    string? DisplayName,
    VenueActivity Activity,
    long Tick);

public sealed record VenueActivityResult(
    bool Applied,
    string? Code,
    string? Message,
    IReadOnlyList<string> Recipients,
    VenueActivityNotice? Notice)
{
    public static VenueActivityResult Reject(string code, string message) =>
        new(false, code, message, Array.Empty<string>(), null);
}

/// <summary>
/// Venue activities are short social status actions. They are admitted only
/// for occupants, obey the venue mask and a per-player cooldown, and never mint
/// food or money: ShareMeal removes a real household pantry unit.
/// </summary>
public sealed class VenueActivityService
{
    private readonly object gate = new();
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly PlayerRegistry players;
    private readonly VenueService venues;
    private readonly SocialGraphService social;
    private readonly VenueOptions options;
    private readonly Dictionary<string, long> lastActivity = new(StringComparer.Ordinal);
    private readonly Dictionary<Entity, List<VenueActivityNotice>> recent = new();

    public VenueActivityService(
        World world,
        EconomyContext economy,
        PlayerRegistry players,
        VenueService venues,
        SocialGraphService social,
        VenueOptions? options = null)
    {
        this.world = world;
        this.economy = economy;
        this.players = players;
        this.venues = venues;
        this.social = social;
        this.options = options ?? venues.Options;
    }

    public VenueActivityResult TryPerform(
        string playerId,
        Entity venue,
        VenueActivity activity,
        long tick)
    {
        if (!players.TryGet(playerId, out var profile) ||
            profile.ControlledSim == Entity.Null ||
            !world.IsAlive(profile.ControlledSim) ||
            world.Has<Dead>(profile.ControlledSim))
            return VenueActivityResult.Reject("not_available", "Your character is not available.");

        if (!venues.IsOccupant(playerId, venue))
            return VenueActivityResult.Reject("not_in_venue", "You are not inside that venue.");
        if (!venues.IsOpen(venue, tick))
            return VenueActivityResult.Reject("venue_closed", "The venue is closed.");

        var data = world.Get<SocialVenue>(venue);
        if ((data.Activities & ToMask(activity)) == 0)
            return VenueActivityResult.Reject("activity_not_allowed", "That activity is not allowed here.");

        lock (gate)
        {
            if (lastActivity.TryGetValue(playerId, out var last)
                && tick - last < options.ActivityCooldownTicks)
                return VenueActivityResult.Reject("activity_cooldown", "Wait a moment before another activity.");
        }

        var sim = profile.ControlledSim;
        if (activity == VenueActivity.ShareMeal && !TryConsumeMeal(sim))
            return VenueActivityResult.Reject("no_food", "You have no food to share.");

        var displayName = world.Has<GenName>(sim) ? world.Get<GenName>(sim).Name : null;
        var notice = new VenueActivityNotice(playerId, displayName, activity, tick);
        var recipients = Recipients(venue, playerId);

        lock (gate)
        {
            lastActivity[playerId] = tick;
            if (!recent.TryGetValue(venue, out var list))
                recent[venue] = list = new List<VenueActivityNotice>();
            list.Add(notice);
            list.RemoveAll(item => tick - item.Tick > options.RecentActivityTicks);
            if (list.Count > options.RecentActivityLimit)
                list.RemoveRange(0, list.Count - options.RecentActivityLimit);
        }

        economy.Emit(new VenueActivityPerformed(
            NetworkIdOf(venue),
            NetworkIdOf(sim),
            activity.ToString(),
            tick));

        return new VenueActivityResult(true, null, null, recipients, notice);
    }

    public IReadOnlyList<VenueActivityNotice> Recent(
        Entity venue,
        string viewerPlayerId,
        long tick)
    {
        lock (gate)
        {
            if (!recent.TryGetValue(venue, out var list))
                return Array.Empty<VenueActivityNotice>();

            return list
                .Where(item => tick - item.Tick <= options.RecentActivityTicks)
                .Where(item => !social.IsBlocked(viewerPlayerId, item.PlayerId))
                .Where(item => !social.IsMuted(viewerPlayerId, item.PlayerId))
                .ToArray();
        }
    }

    private bool TryConsumeMeal(Entity sim)
    {
        var household = LifeSystemHelpers.HouseholdOf(world, economy, sim);
        if (household == Entity.Null ||
            !world.IsAlive(household) ||
            !world.Has<HouseholdPantry>(household))
            return false;

        var pantry = world.Get<HouseholdPantry>(household);
        if (pantry.FoodUnits <= 0)
            return false;

        pantry.FoodUnits--;
        world.Get<HouseholdPantry>(household) = pantry;
        return true;
    }

    private IReadOnlyList<string> Recipients(Entity venue, string playerId) =>
        venues.Occupants(venue)
            .Where(other => !string.Equals(other, playerId, StringComparison.Ordinal))
            .Where(other => !social.IsBlocked(playerId, other) && !social.IsBlocked(other, playerId))
            .Where(other => !social.IsMuted(other, playerId))
            .ToArray();

    private static VenueActivityMask ToMask(VenueActivity activity) => activity switch
    {
        VenueActivity.Sit => VenueActivityMask.Sit,
        VenueActivity.Talk => VenueActivityMask.Talk,
        VenueActivity.ShareMeal => VenueActivityMask.ShareMeal,
        VenueActivity.PlayGame => VenueActivityMask.PlayGame,
        VenueActivity.Cheer => VenueActivityMask.Cheer,
        _ => VenueActivityMask.None
    };

    private ulong NetworkIdOf(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0;
}
