# A05 — Client asset pipeline and rendering

**Phase:** 5 — Client
**Depends:** A03, A04, V3 renderer
**Estimate:** 2–3 sessions
**Status:** Planned (V5)

## Goal

Render appearance in the browser from compact server data and shared atlas parts, within the existing snapshot, frame and memory budgets.

## Context

The client currently renders sims as pooled circles in `clients/browser/src/render/sim-layer.ts`. The asset loader preloads a tileset and an optional sim atlas, but there is no appearance composition, palette system or descriptor cache.

`SimStatus` is delta-replicated. Appearance must be compact and stable so it does not turn every delta into a full payload.

## Design

### Wire contract

Add a compact appearance block to `SimStatus`:

```csharp
public sealed record AppearanceDto
{
    public byte SkinTone { get; init; }
    public byte FaceShape { get; init; }
    public byte HairColor { get; init; }
    public byte HairStyle { get; init; }
    public byte EyeColor { get; init; }
    public byte BodyFrame { get; init; }
    public byte HeightScale { get; init; }
    public byte AgeBand { get; init; }
    public byte Expression { get; init; }
    public byte AppearanceVersion { get; init; }
}
```

- The DTO is numeric and fixed-size.
- It is included in `SimStatus` and in delta snapshots.
- The client validates enum ranges and falls back to a neutral descriptor.
- Appearance does not include traits or private family data.

### Assets

- Extend the asset manifest with `sim-atlas`, `sim-palettes` and optional `portraits`.
- The atlas contains shared regions for bodies, faces, eyes, hair and outfits.
- Palettes are JSON colour ramps, not textures.
- A missing optional asset produces a labelled token and a diagnostic, not a failed load.
- The atlas is versioned by `LayerOrderVersion`; a mismatch forces a reload.

### Rendering

- `AppearanceSpritePool` reuses containers by descriptor hash.
- The sim layer composes layers once per descriptor and updates position/scale per frame.
- Local player and other players use outline/marker overlays, not appearance alone.
- Off-screen sprites are pooled; composition is cached with a bounded LRU.
- Appearance changes (age band, version) invalidate only the affected sprite.
- The render loop never composes inside the network callback; work is queued for the next frame.

### Accessibility

- A sim label can include a short non-visual identity (name, role) for the HUD and screen-reader text.
- High-contrast mode adds a shape/outline cue.
- Reduced motion disables appearance animation, not identity.

## Tasks

- [ ] Add `AppearanceDto` to the contracts and `SimStatus`.
- [ ] Extend the asset manifest and preload with the sim atlas and palettes.
- [ ] Implement `AppearanceSpritePool` and the composition cache.
- [ ] Replace the circle-only sim layer with layered sprites.
- [ ] Add local/other player markers and accessibility cues.
- [ ] Add a fallback for missing atlas parts.
- [ ] Add Vitest tests for composition, cache and fallback.
- [ ] Add Playwright visual smoke coverage.

## Files

- `Genelife.Contracts/Dtos/AppearanceDto.cs`
- `Genelife.Contracts/Dtos/SimStatus.cs`
- `clients/browser/src/assets/preload.ts`
- `clients/browser/src/render/sim-layer.ts`
- new `clients/browser/src/render/appearance/*`
- `clients/browser/src/test/appearance.test.ts`
- `clients/browser/e2e/appearance.spec.ts`

## Acceptance

- Sims render from the appearance descriptor, not a single tint.
- The atlas and palettes load once; no per-sim texture is requested.
- A missing optional asset falls back without breaking the scene.
- The local player and other players are distinguishable without colour alone.
- Appearance changes do not allocate inside the network callback.
- The client stays within the frame and payload budgets with many distinct sims.
- Typecheck, lint, unit tests and build pass.

## Testing

- Compose every age band and body frame.
- Assert the sprite pool reuses a descriptor.
- Assert a version change invalidates the cache.
- Assert a missing atlas part falls back.
- Measure a 2,000-sim viewport composition time.
- Playwright smoke: connect, render, age-band change, reconnect.
