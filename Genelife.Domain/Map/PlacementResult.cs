namespace Genelife.Map;

public readonly record struct PlacementResult(
    bool Success,
    Placement Placement,
    PlacementError Error,
    string? Message,
    LotId? Lot)
{
    public static PlacementResult Ok(Placement placement, LotId? lot = null) =>
        new(true, placement, PlacementError.None, null, lot);

    public static PlacementResult Fail(PlacementError error, string? message = null) =>
        new(false, default, error, message ?? error.ToString(), null);
}
