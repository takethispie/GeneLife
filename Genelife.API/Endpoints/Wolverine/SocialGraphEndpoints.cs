using Genelife.Api.Auth;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Family;
using Genelife.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

/// <summary>
/// Player-owned social edges and reports. Every route is scoped to the caller;
/// admin routes expose the full graph for moderation and audit.
/// </summary>
public static class SocialGraphEndpoints
{
    [WolverineGet("/api/social/relationships")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static PlayerRelationshipStatusDto Relationships(
        SimulationEngine engine,
        HttpContext http)
    {
        var playerId = http.User.GetPlayerId() ?? string.Empty;
        var status = engine.PlayerRelationships?.BuildStatus(playerId, engine.CurrentTick);
        if (status is null)
            return new PlayerRelationshipStatusDto(
                playerId, null, null, null, false, false, Array.Empty<PlayerRelationshipInvitationStatus>());

        return new PlayerRelationshipStatusDto(
            status.PlayerId,
            status.PartnerPlayerId,
            status.PartnerGenosNetworkId,
            status.PartnerName,
            status.SharesHousehold,
            status.CanCohabit,
            status.Invitations
                .Select(invitation => new PlayerRelationshipInvitationStatus(
                    invitation.Id,
                    invitation.FromPlayerId,
                    invitation.ToPlayerId,
                    invitation.Kind.ToString(),
                    invitation.Status.ToString(),
                    invitation.ExpiresTick,
                    invitation.CreatedTick))
                .ToArray());
    }

    [WolverineGet("/api/social/graph")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static SocialOverview Graph(
        SocialGraphService graph,
        SimulationEngine engine,
        HttpContext http) =>
        graph.Overview(http.User.GetPlayerId() ?? string.Empty, engine.CurrentTick);

    [WolverinePost("/api/social/friends/requests")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static SocialRequestResult RequestFriend(
        SocialTargetRequest request,
        SocialGraphService graph,
        SimulationEngine engine,
        HttpContext http) =>
        graph.RequestFriend(
            http.User.GetPlayerId() ?? string.Empty,
            request.TargetPlayerId,
            engine.CurrentTick);

    [WolverinePost("/api/social/friends/requests/{requestId}/accept")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static SocialRequestResult AcceptFriendRequest(
        Guid requestId,
        SocialGraphService graph,
        SimulationEngine engine,
        HttpContext http) =>
        graph.RespondToFriendRequest(
            requestId,
            http.User.GetPlayerId() ?? string.Empty,
            accept: true,
            engine.CurrentTick);

    [WolverinePost("/api/social/friends/requests/{requestId}/decline")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static SocialRequestResult DeclineFriendRequest(
        Guid requestId,
        SocialGraphService graph,
        SimulationEngine engine,
        HttpContext http) =>
        graph.RespondToFriendRequest(
            requestId,
            http.User.GetPlayerId() ?? string.Empty,
            accept: false,
            engine.CurrentTick);

    [WolverinePost("/api/social/friends/requests/{requestId}/cancel")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static SocialRequestResult CancelFriendRequest(
        Guid requestId,
        SocialGraphService graph,
        HttpContext http) =>
        graph.CancelFriendRequest(requestId, http.User.GetPlayerId() ?? string.Empty);

    [WolverineDelete("/api/social/friends/{targetPlayerId}")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult RemoveFriend(
        string targetPlayerId,
        SocialGraphService graph,
        HttpContext http)
    {
        var result = graph.RemoveFriend(http.User.GetPlayerId() ?? string.Empty, targetPlayerId);
        return result.Accepted ? Results.NoContent() : Results.NotFound(new { code = result.Code });
    }

    [WolverinePost("/api/social/blocks")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static SocialEdgeResult Block(
        SocialTargetRequest request,
        SocialGraphService graph,
        SimulationEngine engine,
        HttpContext http) =>
        graph.Block(
            http.User.GetPlayerId() ?? string.Empty,
            request.TargetPlayerId,
            engine.CurrentTick,
            SocialEdgeSource.Player);

    [WolverineDelete("/api/social/blocks/{targetPlayerId}")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult Unblock(
        string targetPlayerId,
        SocialGraphService graph,
        HttpContext http)
    {
        var result = graph.Unblock(http.User.GetPlayerId() ?? string.Empty, targetPlayerId);
        return result.Accepted ? Results.NoContent() : Results.NotFound(new { code = result.Code });
    }

    [WolverinePost("/api/social/mutes")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static SocialEdgeResult Mute(
        SocialTargetRequest request,
        SocialGraphService graph,
        SimulationEngine engine,
        HttpContext http) =>
        graph.Mute(
            http.User.GetPlayerId() ?? string.Empty,
            request.TargetPlayerId,
            engine.CurrentTick,
            SocialEdgeSource.Player);

    [WolverineDelete("/api/social/mutes/{targetPlayerId}")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult Unmute(
        string targetPlayerId,
        SocialGraphService graph,
        HttpContext http)
    {
        var result = graph.Unmute(http.User.GetPlayerId() ?? string.Empty, targetPlayerId);
        return result.Accepted ? Results.NoContent() : Results.NotFound(new { code = result.Code });
    }

    /// <summary>
    /// Creates a report. Chat reports capture their evidence server-side from
    /// the stored message so the client cannot forge the snapshot.
    /// </summary>
    [WolverinePost("/api/social/reports")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static async Task<SocialReportResult> Report(
        SocialReportRequest request,
        ChatService chat,
        SocialGraphService graph,
        SimulationEngine engine,
        HttpContext http)
    {
        var reporter = http.User.GetPlayerId() ?? string.Empty;
        var tick = engine.CurrentTick;

        if (request.MessageId is { } messageId)
            return await chat.ReportAsync(reporter, messageId, request.Reason, tick);

        if (string.IsNullOrWhiteSpace(request.TargetPlayerId))
            return SocialReportResult.Reject("target_required", "Choose a player to report.");

        return graph.Report(
            reporter,
            request.TargetPlayerId,
            request.Reason,
            messageId: null,
            evidence: null,
            tick);
    }

    [WolverineGet("/api/social/admin/graph")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static SocialGraphSnapshot AdminGraph(SocialGraphService graph) => graph.Export();

    [WolverineGet("/api/social/admin/reports")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IReadOnlyList<SocialReport> AdminReports(
        SocialGraphService graph,
        Guid? messageId = null) =>
        graph.Reports(messageId);

    [WolverinePut("/api/social/admin/reports/{reportId}/status")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult SetReportStatus(
        Guid reportId,
        SocialReportStatusRequest request,
        SocialGraphService graph) =>
        graph.SetReportStatus(reportId, request.Status)
            ? Results.NoContent()
            : Results.NotFound();
}

public sealed record SocialTargetRequest(string TargetPlayerId);

public sealed record SocialReportRequest(
    string? TargetPlayerId,
    ReportReason Reason,
    Guid? MessageId = null);

public sealed record SocialReportStatusRequest(ReportStatus Status);
