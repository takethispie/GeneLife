namespace Genelife.Map;

/// <summary>
/// Static configuration for one world map. The map is centred on the origin so
/// the existing float world-space convention and snapshot coordinates remain
/// compatible.
/// <para>
/// <see cref="Width"/> and <see cref="Height"/> are counts of <b>tiles</b>, and
/// <see cref="TileSize"/> is fixed at <c>1f</c> — so one tile is one world unit
/// and the extent in tiles equals the extent in world units. This settles the
/// unit question left open in
/// <c>docs/research/world-scale-migration-32000.md</c> §0.1.
/// </para>
/// <para>
/// The size is configuration: it must be read from here and never hardcoded in
/// game logic (the V3 rule), so a future resize is a config change rather than a
/// code change.
/// </para>
/// </summary>
public sealed record MapOptions
{
    /// <summary>
    /// The product's default world extent — the size a world gets when one is
    /// created without an explicit size. This is the number flipped to
    /// <c>32000×32000</c> by Phase 4 of
    /// <c>docs/research/world-scale-migration-32000.md</c> §5.2, per the PO
    /// ruling that the client's city map <b>is</b> the simulation's world.
    /// </summary>
    public static readonly MapOptions Default = new();

    /// <summary>
    /// The engine's pre-creation placeholder world. A fresh <c>SimulationEngine</c>
    /// (a unit test, or the idle admin console before any map is created) gets
    /// this instead of <see cref="Default"/>: materialising a real 32000×32000
    /// world is the deliberate act of creating a world, not the cost of starting
    /// an empty engine. It stays small on purpose — see the phase-4 PR note on
    /// 32000 generation cost.
    /// </summary>
    public static readonly MapOptions Scaffold = new() { Width = 256, Height = 256 };

    public string MapId { get; init; } = "world";
    public int Version { get; init; } = 1;
    public int Seed { get; init; } = 1;
    public int Width { get; init; } = 32000;
    public int Height { get; init; } = 32000;
    public float TileSize { get; init; } = 1f;

    public MapBounds Bounds => new(
        -(Width / 2),
        -(Height / 2),
        Width - 1 - (Width / 2),
        Height - 1 - (Height / 2));

    public TileCoord ToTile(float x, float y) =>
        new((int)MathF.Round(x / TileSize), (int)MathF.Round(y / TileSize));

    public (float X, float Y) ToWorld(TileCoord tile) =>
        (tile.X * TileSize, tile.Y * TileSize);

    public (float X, float Y) ToWorld(TileCoord tile, float progressX, float progressY) =>
        ((tile.X + progressX) * TileSize, (tile.Y + progressY) * TileSize);
}
