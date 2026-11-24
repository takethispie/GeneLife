using Arch.Core;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Components.Social;
using Genelife.Social;

namespace Genelife.Api.Social;

/// <summary>
/// Projects the live social graph, venue occupancy, reports, moderation audit
/// and chat retention into one admin read model.
/// </summary>
public static class SocialReadModel
{
    public static async Task<SocialAdminOverviewDto> BuildAsync(
        SimulationEngine engine,
        PlayerDirectoryService directory,
        SocialGraphService social,
        ModerationService moderation,
        SocialPrivacyRegistry privacy,
        ChatService chat,
        ChatOptions chatOptions,
        CancellationToken cancellationToken = default)
    {
        var tick = engine.CurrentTick;
        var venues = new List<SocialVenueAdminDto>();
        if (engine.Venues is { } venueService && engine.World is { } world)
        {
            foreach (var entity in venueService.ConfiguredVenues())
            {
                if (!world.IsAlive(entity) || !world.Has<SocialVenue>(entity))
                    continue;
                var data = world.Get<SocialVenue>(entity);
                venues.Add(new SocialVenueAdminDto(
                    NetworkIdOf(world, entity),
                    VenueName(world, entity),
                    data.Kind.ToString(),
                    venueService.Occupancy(entity),
                    data.Capacity,
                    venueService.IsOpen(entity, tick),
                    data.Closed));
            }
        }

        var reports = social.Reports()
            .Select(report => new SocialReportAdminDto(
                report.Id,
                report.ReporterPlayerId,
                report.TargetPlayerId,
                report.Reason.ToString(),
                report.MessageId,
                report.Evidence,
                report.CreatedTick,
                report.Status.ToString()))
            .ToArray();
        var edges = social.AdminEdges()
            .Select(edge => new SocialEdgeAdminDto(
                edge.FromPlayerId,
                edge.ToPlayerId,
                edge.Kind.ToString(),
                edge.SinceTick,
                edge.Source.ToString()))
            .ToArray();
        var requests = social.AdminRequests()
            .Select(request => new SocialRequestAdminDto(
                request.Id,
                request.FromPlayerId,
                request.ToPlayerId,
                request.Kind.ToString(),
                request.Status.ToString(),
                request.CreatedTick,
                request.ExpiresTick))
            .ToArray();
        var partnerships = engine.PlayerRelationships?.ExportInvitations()
            .Select(invitation => new PlayerRelationshipInvitationAdminDto(
                invitation.Id,
                invitation.FromPlayerId,
                invitation.ToPlayerId,
                invitation.Kind.ToString(),
                invitation.Status.ToString(),
                invitation.CreatedTick,
                invitation.ExpiresTick))
            .ToArray() ?? [];
        var actions = moderation.Actions(take: 200)
            .Select(action => new ModerationActionAdminDto(
                action.Id,
                action.ReportId,
                action.ReversesActionId,
                action.ModeratorId,
                action.TargetPlayerId,
                action.CounterpartyPlayerId,
                action.Kind.ToString(),
                action.DurationTicks,
                action.CreatedTick,
                action.EndsTick,
                action.Reason,
                action.Evidence,
                action.MessageId,
                action.VenueNetworkId))
            .ToArray();
        var privacyRows = privacy.Export()
            .Select(setting => new SocialPrivacyAdminDto(
                setting.PlayerId,
                setting.ShowDisplayName,
                setting.AllowNearbyForNonFriends,
                setting.FriendsOnlyMessages))
            .ToArray();
        var status = await chat.StatusAsync(DateTimeOffset.UtcNow, cancellationToken);

        return new SocialAdminOverviewDto(
            tick,
            directory.AdminPresence(),
            venues,
            reports,
            edges,
            requests,
            partnerships,
            actions,
            privacyRows,
            new ChatRetentionAdminDto(
                status.RetainedMessages,
                status.DeletedMessages,
                status.OldestRetained,
                status.CheckedAt,
                chatOptions.RetentionDays));
    }

    private static string VenueName(World world, Entity venue)
    {
        if (world.Has<Property>(venue))
            return world.Get<Property>(venue).Name;
        if (world.Has<Office>(venue))
            return world.Get<Office>(venue).OfficeName;
        if (world.Has<School>(venue))
            return world.Get<School>(venue).Name;
        if (world.Has<GenName>(venue))
            return world.Get<GenName>(venue).Name;
        return "Social venue";
    }

    private static ulong NetworkIdOf(World world, Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0;
}
