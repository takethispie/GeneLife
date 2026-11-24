using Genelife.Api.Auth;
using Genelife.Api.Services;
using Genelife.Api.Social;
using Genelife.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

/// <summary>
/// Bounded social presence, chat history and report capture. Players only see
/// nearby or same-venue records; admin sees the full online presence list and
/// moderation reads. No private simulation state is exposed.
/// </summary>
public static class SocialEndpoints
{
    [WolverineGet("/api/social/presence")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static PlayerDirectoryDto Presence(
        PlayerDirectoryService directory,
        HttpContext http,
        string scope = "nearby",
        int radius = 12)
    {
        var parsed = Enum.TryParse<PresenceScope>(scope, ignoreCase: true, out var presenceScope)
            ? presenceScope
            : PresenceScope.Nearby;
        var playerId = http.User.GetPlayerId() ?? string.Empty;
        return directory.Directory(playerId, parsed, radius);
    }

    [WolverineGet("/api/social/admin/presence")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IReadOnlyList<PlayerPresenceDto> AdminPresence(PlayerDirectoryService directory) =>
        directory.AdminPresence();

    [WolverineGet("/api/social/privacy")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static SocialPrivacyDto Privacy(PlayerDirectoryService directory, HttpContext http) =>
        directory.GetPrivacy(http.User.GetPlayerId() ?? string.Empty);

    [WolverinePut("/api/social/privacy")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static SocialPrivacyDto SetPrivacy(
        SocialPrivacyUpdateRequest request,
        PlayerDirectoryService directory,
        HttpContext http)
    {
        var playerId = http.User.GetPlayerId() ?? string.Empty;
        directory.SetPrivacy(playerId, new SocialPrivacyDto(
            request.ShowDisplayName,
            request.AllowNearbyForNonFriends));
        return directory.GetPrivacy(playerId);
    }

    /// <summary>Recent retained chat for the caller's allowed scope.</summary>
    [WolverineGet("/api/social/chat")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static Task<IReadOnlyList<ChatMessage>> ChatHistory(
        ChatService chat,
        HttpContext http,
        string channel = "say",
        string? targetPlayerId = null,
        int take = 50) =>
        chat.ReadAsync(
            http.User.GetPlayerId() ?? string.Empty,
            ParseChannel(channel),
            string.IsNullOrWhiteSpace(targetPlayerId) ? null : targetPlayerId,
            take);

    /// <summary>Captures report evidence for a chat message.</summary>
    [WolverinePost("/api/social/chat/report")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static Task<SocialReportResult> ReportChat(
        ChatReportRequest request,
        ChatService chat,
        SimulationEngine engine,
        HttpContext http) =>
        chat.ReportAsync(
            http.User.GetPlayerId() ?? string.Empty,
            request.MessageId,
            request.Reason,
            engine.CurrentTick);

    [WolverineGet("/api/social/admin/chat/reports")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IReadOnlyList<SocialReport> ChatReports(
        SocialGraphService graph,
        Guid? messageId = null) =>
        graph.Reports(messageId);

    [WolverineDelete("/api/social/admin/chat/{messageId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static async Task<IResult> RemoveChat(ChatService chat, Guid messageId)
    {
        var removed = await chat.RemoveAsync(messageId);
        return removed ? Results.NoContent() : Results.NotFound();
    }

    private static ChatChannel ParseChannel(string channel) =>
        Enum.TryParse<ChatChannel>(channel, ignoreCase: true, out var parsed)
            ? parsed
            : ChatChannel.Say;
}

public sealed record ChatReportRequest(Guid MessageId, ReportReason Reason = ReportReason.Other);
