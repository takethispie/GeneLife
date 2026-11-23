# HUD without a framework

The world is a Pixi canvas. The HUD is ordinary DOM. That is a deliberate simplification, not a temporary accident.

## Why no React in the first slice

- The HUD is small: connection state, player, selection, notifications, settings.
- DOM/CSS gives crisp text, accessibility and fast iteration with no component runtime.
- React adds a second state model next to the simulation store.
- React must never own the render loop or thousands of entity sprites.

## When React becomes reasonable

Add React only when the HUD grows into many stateful panels, forms or lists that are painful to update by hand. Even then:

- React renders the HUD around the canvas, not the world.
- The simulation store remains outside React.
- HUD updates remain coalesced and low-frequency.
- Pixi owns sprites and camera transforms.

## Why not Redux

Redux is a poor fit for a 25 Hz entity stream. If the HUD later needs predictable UI state, a small UI store or Zustand is enough. The simulation store stays mutable and separate.

## Product consequence

V3 ships without a frontend framework. If that becomes a constraint, the migration boundary is the HUD only; the world and netcode do not change.
