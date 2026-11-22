# R06 — Admin map UI

**Phase:** 4 — Admin
**Depends:** M09, R02, R05
**Estimate:** 3 sessions
**Status:** Planned (V2)

## Goal

Give operators a map console that shows the generated world, building footprints, entries, occupancy and live player positions, and lets them attempt or validate a placement without touching the world directly.

## Context

The Blazor WebAssembly admin has data, economy, housing and lifecycle pages. They poll `GameDataService`, show summary cards and tabs, and use command results. The map page should follow the same pattern.

V2 is not a graphics client. A simple SVG or canvas grid is enough to make the map inspectable and to expose placement errors.

## Design

### Page structure

`Pages/Map.razor`:

- summary cards: map id/version, buildings, occupied tiles, free walkable tiles, unplaced buildings, placement issues;
- map canvas: terrain/roads, building rectangles by kind, entries, player markers;
- pan and zoom, with the server viewport used for large maps;
- selection panel: building name, kind, district, footprint, entry, owner;
- filters: kind, district, occupied/free, player-controlled;
- placement form: kind, district, anchor, footprint, entry;
- validation result panel with error code and message;
- links from Data/Economy/Housing/Lifecycle rows.

### Data flow

- `GameDataService.GetMapAsync()` for metadata and summary;
- `GameDataService.GetMapViewportAsync(...)` for a bounded map region;
- `GameDataService.GetMapBuildingsAsync(...)` for filtered lists;
- placement uses the existing admin command endpoint and polls `CommandResult`/read model;
- live player positions come from `/api/data` or `SendSnapshotDelta` if the page subscribes to the hub;
- no direct world access, no browser-side mutation.

### Rendering

Use an SVG or canvas grid with:

- a tile layer for roads/water/land;
- a building layer for footprints colored by kind;
- an entry marker and label;
- a player layer with the controlled sim highlighted;
- a hit target per building for selection.

Performance rules:

- do not render 65k individual DOM nodes for a full map;
- render the visible viewport only;
- use simple shapes and stable layer order;
- stop animation when the simulation is stopped.

### Error handling

- stopped simulation: show the last map and positions with a clear banner;
- map version changed: refresh the map before applying a placement;
- command rejected: show the typed error (`Occupied`, `OutOfBounds`, ...);
- API unavailable: preserve the last view and show the existing error banner pattern.

## Tasks

- [ ] Add `Pages/Map.razor` and navigation entry.
- [ ] Add `GameDataService` map/viewport/building methods.
- [ ] Implement SVG/canvas viewport rendering with layers.
- [ ] Add summary cards, filters and selection panel.
- [ ] Add placement form and command-result feedback.
- [ ] Add map version refresh handling.
- [ ] Add stopped-simulation and error states.
- [ ] Add cross-links from existing pages.
- [ ] Add component render and contract tests.

## Files

- new `Genelife.Web/Pages/Map.razor`, `Map.razor.css`
- `Genelife.Web/Services/GameDataService.cs`
- `Genelife.Web/Layout/NavMenu.razor`
- `Genelife.Web/Pages/Data.razor`, `Economy.razor`, `Housing.razor`, `Lifecycle.razor`
- `Genelife.UnitTests/Web/MapPageTests.cs`

## Acceptance

- The page renders a generated map with building footprints and entries.
- Live player positions appear and update without a full page reload.
- A selected building shows its placement and owner.
- An invalid placement displays the specific error.
- A valid placement appears after the command is applied.
- Filters and viewport bounds work.
- Stopped simulation and API errors are handled visibly.
- The page does not fetch the entire tile grid as DOM nodes.

## Testing

- Component render test with a fixed map DTO.
- Viewport query test.
- Placement success/failure flow.
- Stopped-simulation state.
- Filter test.
- Navigation/link test.
