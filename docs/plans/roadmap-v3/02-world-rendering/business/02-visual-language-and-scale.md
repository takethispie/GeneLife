# Visual language and scale

The client must stay readable at 256×256 tiles while still showing individual sims and building entrances.

## Readability priorities

1. **Terrain and roads** establish orientation.
2. **Buildings** show type, footprint and entrance.
3. **The local player** is unmistakable.
4. **Nearby sims** are visible but do not drown the player.
5. **Selection and targets** are transient and high-contrast.

## Scale rules

- Use a texture atlas and tinting instead of one texture per building or sim variant.
- Prefer silhouettes and colour families over detailed art in the first slice.
- Keep labels sparse; show them on hover or at closer zoom levels.
- Do not render every NPC name at every zoom.
- Use culling and level-of-detail before adding more art.

## Accessibility

- Colour alone must not distinguish road, water, building and selection.
- The HUD must remain readable at 100% browser zoom and with reduced motion.
- A minimap or coordinate readout is optional, but the debug overlay is required for development.

## Product consequence

The first visual pass is successful when a player can navigate without the debug overlay. The overlay exists to prove the client is correct, not to make the game usable.
