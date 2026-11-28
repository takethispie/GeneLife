namespace Genelife.Api.DTOs;

/// <summary>
/// A hub-side signal that a player's action context changed (an action applied, a move, a
/// hire, a lease): the dial should receive a fresh catalogue. The catalogue itself is built by the
/// read model, not by the producer of this notice, so the push carries only the identity.
/// </summary>
public sealed record ActionCatalogueNotice(string PlayerId, long Tick);
