using Arch.Core;
using Genelife.Api.Auth;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Components.Social;
using Genelife.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

/// <summary>
/// Venue directory and moderation hooks. Venue presence is public only to
/// occupants of the same venue; admin routes can close a venue or remove an
/// occupant.
/// </summary>
public static class VenueEndpoints
{
    [WolverineGet("/api/social/venues/current")]
    [Authorize(Policy = AuthPolicies.Player)]
    public static IResult Current(
        SimulationEngine engine,
        SocialGraphService social,
        SocialPrivacyRegistry privacy,
        HttpContext http)
    {
        var playerId = http.User.GetPlayerId() ?? string.Empty;
        var venues = engine.Venues;
        var world = engine.World;
        if (venues is null || world is null)
            return Results.NoContent();

        var venue = venues.VenueOf(playerId);
        if (venue == Entity.Null || !world.IsAlive(venue) || !world.Has<SocialVenue>(venue))
            return Results.NoContent();

        var data = world.Get<SocialVenue>(venue);
        var occupants = new List<VenueOccupantDto>();
        foreach (var occupant in venues.Occupants(venue))
        {
            if (string.Equals(occupant, playerId, StringComparison.Ordinal))
                continue;
            if (social.IsBlocked(playerId, occupant))
                continue;

            var name = "Resident";
            ulong simNetworkId = 0;
            if (engine.Players.TryGet(occupant, out var profile))
            {
                if (profile.ControlledSim != Entity.Null && world.IsAlive(profile.ControlledSim))
                {
                    simNetworkId = NetworkIdOf(world, profile.ControlledSim);
                    if (world.Has<GenName>(profile.ControlledSim) &&
                        privacy.For(occupant).ShowDisplayName)
                        name = world.Get<GenName>(profile.ControlledSim).Name;
                }
            }

            occupants.Add(new VenueOccupantDto(occupant, name, simNetworkId));
        }

        var recent = engine.VenueActivities?.Recent(venue, playerId, engine.CurrentTick)
            ?? Array.Empty<VenueActivityNotice>();

        return Results.Ok(new VenueStatusDto(
            NetworkIdOf(world, venue),
            VenueName(world, venue),
            data.Kind.ToString(),
            venues.Occupancy(venue),
            data.Capacity,
            venues.IsOpen(venue, engine.CurrentTick),
            data.Closed,
            data.Activities.ToString(),
            occupants,
            recent.Select(item => new VenueActivityNoticeDto(
                item.PlayerId,
                item.DisplayName,
                item.Activity.ToString(),
                item.Tick)).ToArray()));
    }

    [WolverinePost("/api/social/admin/venues/{networkId}/close")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Close(ulong networkId, SimulationEngine engine) =>
        SetClosed(networkId, engine, closed: true);

    [WolverinePost("/api/social/admin/venues/{networkId}/open")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Open(ulong networkId, SimulationEngine engine) =>
        SetClosed(networkId, engine, closed: false);

    [WolverineDelete("/api/social/admin/venues/{networkId}/occupants/{playerId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult RemoveOccupant(ulong networkId, string playerId, SimulationEngine engine)
    {
        var venues = engine.Venues;
        if (venues is null)
            return Results.NotFound();

        var venue = venues.FindByNetworkId(networkId);
        return venue != Entity.Null && venues.RemoveOccupant(playerId, venue, engine.CurrentTick)
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static IResult SetClosed(ulong networkId, SimulationEngine engine, bool closed)
    {
        var venues = engine.Venues;
        if (venues is null)
            return Results.NotFound();

        var venue = venues.FindByNetworkId(networkId);
        return venue != Entity.Null && venues.SetClosed(venue, closed, engine.CurrentTick)
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static string VenueName(World world, Entity venue)
    {
        if (world.Has<Property>(venue))
            return world.Get<Property>(venue).Name;
        if (world.Has<Office>(venue))
            return world.Get<Office>(venue).OfficeName;
        if (world.Has<School>(venue))
            return world.Get<School>(venue).Name;
        return "Social venue";
    }

    private static ulong NetworkIdOf(World world, Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0;
}
