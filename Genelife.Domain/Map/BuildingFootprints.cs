namespace Genelife.Map;

/// <summary>
/// Per-kind building footprints, sized to hold a real interior floor plan
/// rather than to an envelope around the building's content count.
///
/// This is the derivation of <c>docs/research/building-sizing-and-world-scale.md</c>
/// Part 1 (the MINIMUM VIABLE column of the per-kind table). A roof-cutaway
/// interior is an index over the building's own footprint tiles, so the
/// footprint must be able to hold the rooms the domain already asks for:
/// <list type="bullet">
///   <item>one place (unit / shelter bed / office seat / pupil place) per
///   <c>UnitCount</c> or <c>Capacity</c>;</item>
///   <item>each place gets a minimum readable 2x2-tile room (4 tiles);</item>
///   <item>a x1.25 circulation/wall allowance over the raw floor area, applied
///   to the whole interior so the arithmetic is independent of the room split;</item>
///   <item>plus the kind's common area (lobby / meeting room / hall).</item>
/// </list>
///
/// The minimal near-square WxH rectangle that holds that area is the footprint.
/// Sizing to the minimum (not to comfort) is the doc's firm recommendation 5.1:
/// comfort buys headroom the first generic interiors cannot use.
/// </summary>
public static class BuildingFootprints
{
    /// <summary>
    /// Largest footprint dimension any kind may produce. It is the largest
    /// minimum-viable value in the Part-1 table (the 100-pupil school, 23x23).
    /// A lot must be able to contain every footprint, so
    /// <see cref="MapGenerationOptions.RoadSpacing"/> is derived from this and the
    /// two move together: a footprint that fits no lot makes world generation
    /// throw (doc Part 1.3). Inputs whose minimum-viable interior would be larger
    /// than this are clamped, and the interior is laid out densely inside it.
    /// </summary>
    public const int MaxDimension = 23;

    // Minimum readable room: 2x2 tiles = 4 tiles of floor (doc Part 1.2).
    // Circulation/wall allowance: x1.25. The two combine to an integer slope of
    // 5 tiles of floor per occupant place, which is exact for every input.
    private const int TilesPerPlace = 5;

    // Kind common areas (doc Part 1.2): residential/shelter lobby 3x3 = 9;
    // office meeting room 4x4 + lobby 3x3 = 25; school hall 4x4 = 16.
    private const int ResidentialCommonArea = 9;
    private const int OfficeCommonArea = 25;
    private const int SchoolCommonArea = 16;

    /// <summary>
    /// Residential / shelter footprint. <paramref name="units"/> is the property's
    /// <c>UnitCount</c>; for a shelter it is the number of beds.
    /// </summary>
    public static Footprint ForProperty(int units, bool isShelter = false) =>
        FromPlaces(units, ResidentialCommonArea);

    /// <summary>Office footprint sized to hold the company's seats.</summary>
    public static Footprint ForOffice(int capacity) =>
        FromPlaces(capacity, OfficeCommonArea);

    /// <summary>School footprint sized to hold the pupil places and the hall.</summary>
    public static Footprint ForSchool(int capacity) =>
        FromPlaces(capacity, SchoolCommonArea);

    private static Footprint FromPlaces(int places, int commonArea)
    {
        var safePlaces = Math.Max(0, places);
        var area = TilesPerPlace * safePlaces + commonArea;

        // Smallest near-square W x H rectangle that holds the area, W >= H.
        var width = (int)Math.Ceiling(Math.Sqrt(area));
        var height = (int)Math.Ceiling(area / (double)width);

        return new Footprint(
            Math.Clamp(width, 1, MaxDimension),
            Math.Clamp(height, 1, MaxDimension));
    }
}
