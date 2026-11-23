using System.Text.Json;
using Genelife.Api.Auth;
using Genelife.Api.Commands;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

/// <summary>
/// Player-facing life endpoints and the admin player-life inspector. Player
/// commands are admitted through <see cref="PlayerCommandService"/> and applied
/// on the tick thread.
/// </summary>
public static class PlayerEndpoints
{
    [WolverinePost("/api/player/actions")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult SubmitAction(
        PlayerActionRequest request,
        PlayerCommandService commands,
        HttpContext http)
    {
        var playerId = http.User.GetPlayerId();
        var admission = commands.Admit(
            playerId,
            request.Kind,
            request.SimNetworkId,
            request.Payload,
            request.Sequence,
            request.CommandId);
        if (!admission.Accepted)
            return Results.Json(new { code = admission.Code, message = admission.Message }, statusCode: 400);

        return Results.Accepted($"/api/player/actions/{admission.CommandId}", new
        {
            commandId = admission.CommandId
        });
    }

    [WolverineGet("/api/player/actions/{commandId}")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult GetAction(Guid commandId, SimulationEngine engine, HttpContext http)
    {
        var playerId = http.User.GetPlayerId();
        var result = engine.GetPlayerCommandResult(commandId);
        if (result is null)
            return Results.NotFound(new { message = "Command not found." });
        if (!string.Equals(result.PlayerId, playerId, StringComparison.Ordinal))
            return Results.NotFound(new { message = "Command not found." });
        return Results.Ok(result);
    }

    [WolverineGet("/api/player/me")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult Me(SimulationEngine engine, HttpContext http)
    {
        var playerId = http.User.GetPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
            return Results.Unauthorized();

        if (engine.GetPlayerSimNetworkId(playerId) == 0)
            engine.EnsurePlayerSim(playerId);

        var life = engine.BuildPlayerLife(playerId);
        if (life is null)
            return Results.NotFound(new { message = "Player sim is not available yet." });

        return Results.Ok(new PlayerMeStatus(
            life,
            engine.BuildPlayerInvitations(playerId),
            life.RecentActions));
    }

    [WolverineGet("/api/player/invitations")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IReadOnlyList<PlayerInvitationStatus> Invitations(
        SimulationEngine engine,
        HttpContext http) =>
        engine.BuildPlayerInvitations(http.User.GetPlayerId() ?? string.Empty);

    [WolverineGet("/api/player")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static PlayerOverviewStatus Overview(SimulationEngine engine) =>
        engine.BuildPlayerOverview();

    [WolverineGet("/api/player/lives")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static PlayerLifePage Lives(
        SimulationEngine engine,
        int take = 100,
        int skip = 0,
        string? search = null,
        string? state = null) =>
        engine.BuildPlayerLives(take, skip, search, state);

    [WolverineGet("/api/player/lives/{playerId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Life(string playerId, SimulationEngine engine)
    {
        var life = engine.BuildPlayerLife(playerId);
        return life is null
            ? Results.NotFound(new { message = "Player not found." })
            : Results.Ok(life);
    }

    [WolverinePost("/api/player/{playerId}/continuation")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult SetContinuation(
        string playerId,
        PlayerContinuationRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Results.BadRequest(new { message = "A reason is required." });

        return Enqueue(engine, new SetPlayerContinuationCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            playerId,
            request.Policy,
            request.Reason));
    }

    [WolverinePost("/api/player/{playerId}/force-continuation")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult ForceContinuation(
        string playerId,
        PlayerContinuationRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Results.BadRequest(new { message = "A reason is required." });

        return Enqueue(engine, new ForcePlayerContinuationCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            playerId,
            request.Policy,
            request.Reason));
    }

    [WolverinePost("/api/player/{playerId}/detach")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Detach(
        string playerId,
        PlayerReasonRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Results.BadRequest(new { message = "A reason is required." });

        return Enqueue(engine, new DetachPlayerSimCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            playerId,
            request.Reason));
    }


    private static IResult Enqueue(SimulationEngine engine, AdminCommand command)
    {
        var commandId = engine.EnqueueAdminCommand(command);
        return Results.Accepted($"/api/commands/{commandId}", new { commandId });
    }
}

public sealed record PlayerActionRequest(
    PlayerActionKind Kind,
    ulong SimNetworkId,
    JsonElement Payload,
    long Sequence,
    Guid? CommandId = null);

public sealed record PlayerContinuationRequest(string Policy, string Reason);

public sealed record PlayerReasonRequest(string Reason);
