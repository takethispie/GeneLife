# A03 — Portrait and sprite composition

**Phase:** 3 — Rendering
**Depends:** A01, A02
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Turn an `AppearanceDescriptor` into a small, ordered layer list that the browser and the admin preview can render without per-sim assets.

## Context

The browser currently draws a circle. PixiJS can compose sprites, tint them and pool them, but only if the server provides a stable layer contract. A portrait and a world sprite should share the same identity even if they use different scales.

## Design

### Layer contract

```csharp
public sealed record AppearanceLayers
{
    public string BodyPart { get; init; }
    public string SkinPalette { get; init; }
    public string FacePart { get; init; }
    public string EyePart { get; init; }
    public string HairPart { get; init; }
    public string OutfitPart { get; init; }
    public string ExpressionPart { get; init; }
    public float HeightScale { get; init; }
    public int LayerOrderVersion { get; init; }
}
```

Part ids reference atlas regions. Palette ids reference shared colour ramps. No part id is a per-sim file.

### Composition rules

- Draw order: body, skin, outfit, face, eyes, hair, expression.
- Skin tone selects a palette ramp; it does not create a new texture.
- Face shape selects a base head region.
- Hair colour selects a palette; hair style selects a region.
- Body frame and height scale adjust proportions.
- Outfit is selected by age band and role (child, student, worker, elder, player).
- Expression is a small overlay derived from temperament.
- The local player gets a non-appearance outline/marker; other players get a distinct marker. Appearance is not the only identity cue.

### Portrait

- A portrait is the same layer list rendered at a larger scale.
- The admin preview uses a deterministic SVG renderer from the same layer list.
- The browser uses Pixi sprites and palettes.
- No portrait image is stored on the server.

### Fallback

- A missing atlas part falls back to a labelled token.
- A missing palette falls back to a neutral ramp.
- A failed composition never blocks the render loop; it is logged once per descriptor.

## Tasks

- [ ] Add `AppearanceLayers` and the pure composition function.
- [ ] Add the part/palette naming contract and validation.
- [ ] Add outfit selection by age and role.
- [ ] Add the local-player and other-player markers as non-appearance overlays.
- [ ] Add the admin SVG renderer from the same layer list.
- [ ] Add fallback behavior and diagnostics.
- [ ] Add snapshot tests for a representative descriptor set.
- [ ] Document the atlas contract.

## Files

- new `Genelife/Appearance/AppearanceLayers.cs`
- new `Genelife/Appearance/AppearanceComposer.cs`
- new `Genelife.Web/Appearance/AppearanceSvgRenderer.cs`
- `clients/browser/src/render/appearance/*`
- `Genelife.UnitTests/Appearance/AppearanceCompositionTests.cs`

## Acceptance

- The same descriptor always produces the same layer list.
- The layer list contains no per-sim asset path.
- Skin tone, hair colour and outfit use palettes/regions, not new textures.
- A missing part falls back without throwing.
- The local player and other players remain distinguishable without colour alone.
- The admin preview and the browser render the same identity.

## Testing

- Compose every age band and body frame.
- Assert layer order and part ids.
- Assert a missing part falls back.
- Assert the SVG renderer and the browser fixture agree on part ids.
- Snapshot a representative family and compare.
