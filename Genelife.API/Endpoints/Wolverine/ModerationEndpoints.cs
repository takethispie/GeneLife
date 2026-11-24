using Genelife.Api.Auth;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Api.Social;
using Genelife.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

/// <summary>
/// Admin moderation queue, audited actions and reversals.
/// </summary>
public static class ModerationEndpoints
{
    [WolverineGet("/api/social/notices")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IReadOnlyList<ModerationNoticeDto> Notices(
        ModerationService moderation,
        SimulationEngine engine,
        HttpContext http) =>
        moderation.Notices(http.User.GetPlayerId() ?? string.Empty, engine.CurrentTick)
            .Select(action => new ModerationNoticeDto(
                action.Id,
                action.Kind.ToString(),
                action.Reason,
                action.CreatedTick,
                action.EndsTick))
            .ToArray();

    [WolverineGet("/api/social/admin/moderation/queue")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static ModerationQueue Queue(ModerationService moderation, SimulationEngine engine) =>
        moderation.Queue(engine.CurrentTick);

    [WolverineGet("/api/social/admin/moderation/actions")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IReadOnlyList<ModerationAction> Actions(
        ModerationService moderation,
        Guid? reportId = null,
        string? targetPlayerId = null,
        int take = 100) =>
        moderation.Actions(reportId, targetPlayerId, take);

    [WolverineGet("/api/social/admin/moderation/offenders")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IReadOnlyList<RepeatOffender> Offenders(
        ModerationService moderation,
        SimulationEngine engine) =>
        moderation.RepeatOffenders(engine.CurrentTick);

    [WolverinePost("/api/social/admin/moderation/actions")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static Task<ModerationResult> Apply(
        ModerationRequest request,
        ModerationService moderation,
        SimulationEngine engine,
        HttpContext http) =>
        moderation.ApplyAsync(
            request,
            http.User.Identity?.Name ?? "admin",
            engine.CurrentTick);

    [WolverinePost("/api/social/admin/moderation/actions/{actionId}/reverse")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static ModerationResult Reverse(
        Guid actionId,
        ModerationReverseRequest request,
        ModerationService moderation,
        SimulationEngine engine,
        HttpContext http) =>
        moderation.Reverse(
            actionId,
            http.User.Identity?.Name ?? "admin",
            request.Reason,
            engine.CurrentTick);
}

public sealed record ModerationReverseRequest(string Reason);
