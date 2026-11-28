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
            request.GenosNetworkId,
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

    /// <summary>
    /// The action catalogue read model (GDD chapter 08 §8.5(1)): for the authenticated player's genos,
    /// the verbs valid now with their refusal code, cost, duration and target hint. Produced by the
    /// same <c>Evaluate</c> the tick drain uses; the client never derives its own action list.
    /// </summary>
    [WolverinePost("/api/player/actions/catalogue")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult ActionCatalogue(
        ActionCatalogueRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        var playerId = http.User.GetPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
            return Results.Unauthorized();

        var catalogue = engine.BuildActionCatalogue(playerId, request);
        if (catalogue is null)
            return Results.Json(
                new { code = PlayerRefusal.WorldNotReady.WireCode(), message = "The world is not ready yet." },
                statusCode: 503);
        return Results.Ok(catalogue);
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

    [WolverinePost("/api/player/respawn")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult Respawn(SimulationEngine engine, HttpContext http)
    {
        var playerId = http.User.GetPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
            return Results.Unauthorized();

        var result = engine.RespawnPlayerAsChild(playerId);
        return result.Success
            ? Results.Ok(result)
            : Results.Json(new { code = result.Code, message = result.Message }, statusCode: 400);
    }

    [WolverineGet("/api/player/me")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult Me(SimulationEngine engine, HttpContext http)
    {
        var playerId = http.User.GetPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
            return Results.Unauthorized();

        if (engine.GetPlayerGenosNetworkId(playerId) == 0)
            engine.EnsurePlayerGenos(playerId);

        var life = engine.BuildPlayerLife(playerId);
        if (life is null)
            return Results.NotFound(new { message = "Player genos is not available yet." });

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

    /// <summary>Operator view of one player's catalogue — the admin-first observability rule (§8.6).</summary>
    [WolverineGet("/api/player/{playerId}/actions/catalogue")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult AdminActionCatalogue(string playerId, SimulationEngine engine)
    {
        var catalogue = engine.BuildActionCatalogue(playerId, new ActionCatalogueRequest(0));
        return catalogue is null
            ? Results.NotFound(new { message = "Player not found." })
            : Results.Ok(catalogue);
    }

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

        return Enqueue(engine, new DetachPlayerGenosCommand(
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
    ulong GenosNetworkId,
    JsonElement Payload,
    long Sequence,
    Guid? CommandId = null);

public sealed record PlayerContinuationRequest(string Policy, string Reason);

public sealed record PlayerReasonRequest(string Reason);
