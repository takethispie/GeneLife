# A04 — Aging and family resemblance

**Phase:** 4 — Rendering
**Depends:** A02, A03
**Estimate:** 1–2 sessions
**Status:** Planned (V5)

## Goal

Make age and family visible in appearance without changing a sim's identity or adding a second inheritance model.

## Context

A sim ages from child to elder over a lifetime. V4 traits are stable, but appearance must change with age: proportions, hair colour, posture and outfit. Family resemblance must come from inherited traits, not from a special family portrait.

## Design

### Age bands

| Band | Age | Visual changes |
|---|---|---|
| Child | 0–12 | smaller body, rounder face, child outfit |
| Teen | 13–17 | taller, teen outfit, adult hair style set |
| Adult | 18–39 | full height, adult outfit |
| Middle | 40–59 | slight posture change, first grey hair |
| Senior | 60+ | grey/white hair, reduced height scale, elder outfit |

- Age bands are derived from `Alive.Age`.
- The transition is deterministic and does not reroll inherited traits.
- Hair colour shifts toward grey in middle and senior bands using a palette rule, not a trait mutation.
- Height scale decreases slightly in the senior band.
- Outfit changes by age band and role.

### Family resemblance

- A child's `SkinTone`, `FaceShape`, `HairColor` and `EyeColor` come from the parents' inherited traits.
- The admin preview can show a parent, child and sibling side by side.
- A resemblance check compares inherited fields, not rendered pixels.
- No family-specific asset or portrait override.

### Player identity

- The player's appearance persists across reconnect and continuation.
- An heir keeps the family traits and may resemble the deceased player.
- A new sim gets a new trait set and a new appearance.

## Tasks

- [ ] Add the age-band derivation and transition table.
- [ ] Add hair greying, height scale and outfit changes by band.
- [ ] Add the family comparison read model and admin view.
- [ ] Add a resemblance check for inherited fields.
- [ ] Add tests for child → adult → senior transitions.
- [ ] Add tests for parent/child/sibling resemblance.
- [ ] Document that aging does not mutate traits.
- [ ] Add a visual fixture for each age band.

## Files

- new `Genelife/Appearance/AgingAppearancePolicy.cs`
- new `Genelife/Appearance/FamilyResemblance.cs`
- `Genelife.Web/Pages/Appearance.razor`
- `clients/browser/src/render/appearance/*`
- `Genelife.UnitTests/Appearance/AgingAppearanceTests.cs`

## Acceptance

- A sim changes age band at the configured ages.
- Hair greys and height scale changes without changing inherited traits.
- A child resembles its parents in inherited fields.
- Siblings share inherited fields without being identical.
- The player's appearance survives reconnect and heir continuation.
- The admin can compare a family side by side.

## Testing

- Age a sim through every band and snapshot the descriptor.
- Assert traits are unchanged by aging.
- Compare parent/child and sibling descriptors.
- Assert a new sim does not inherit the player's appearance.
- Assert the family comparison read model is deterministic.
