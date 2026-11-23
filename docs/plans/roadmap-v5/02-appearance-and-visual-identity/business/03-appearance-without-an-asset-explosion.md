# Appearance without an asset explosion

The naive way to render appearance is one texture per sim. With thousands of sims, generations of children and aging, that becomes an asset pipeline, a memory problem and a cache-invalidation problem at the same time.

V5 uses a small set of parts and palettes instead.

## The composition model

A sim is drawn from layers:

- body frame and height scale;
- skin tone palette;
- face shape;
- eyes and brows;
- hair colour and hair style;
- default outfit by age and role;
- expression from temperament;
- age-band adjustments.

Each layer is a shared atlas region or a tinted shape. The client composes them at draw time and pools the resulting sprites.

## Why this scales

- **Memory.** One atlas serves every sim. There is no per-sim texture.
- **Payload.** The server sends a compact descriptor (a handful of bytes), not an image or a long list of asset ids.
- **Determinism.** The descriptor is derived from traits, so the same sim looks the same after restore.
- **Caching.** Sprites are cached by descriptor hash, not by sim id.
- **Aging.** An age-band change updates a few layers instead of rebuilding a portrait.
- **Fallback.** If an atlas part is missing, the client draws a labelled token rather than failing the whole scene.

## What we deliberately avoid

- A portrait rendered and stored on the server.
- A unique texture per child or per family.
- A client-side copy of the trait model.
- A character creator with arbitrary sliders.
- A cosmetic shop with appearance as a paid advantage.

## Budget

- Descriptor: fixed-size bytes in `SimStatus`.
- Atlas: a small number of shared sheets loaded once.
- Sprite pool: bounded by the visible sim count.
- Composition cache: bounded by the number of distinct descriptors in the viewport, with eviction.

## Success signal

A 2,000-sim viewport with many families and ages renders within the existing frame and payload budgets, and a new child costs a descriptor and a pooled sprite, not a new asset.
