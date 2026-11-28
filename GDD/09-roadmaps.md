# 09 — Roadmaps Ahead

> **Status: Outline — co-authored with @tech-leader.**
> This chapter is the *game-design* view of the plan; the authoritative delivery plan stays
> in `docs/plans/` per the release-roadmap convention.

## 9.1 Strategic phases implied by this GDD
1. **Bridge — client foundation.** Hands the model and contract to the desktop (Godot C#)
   client; retires the prototype renderer without changing the model.
2. **World & space.** World size + building sizing (atomic), so the "open space" and
   interiors are physically possible.
3. **Interiors & personal space.** Player-only cutaway interiors, private first.
4. **Control & UX.** The Sims-grade personal-control layer on the desktop client.
5. **Social depth.** Shared-world features beyond the V5 base.

## 9.2 Constraints on sequencing (already decided)
- **Balancing and performance work is out of scope at the current stage** — roadmaps must not
  schedule it unprompted.
- **World sizing change is atomic** (footprints + road spacing together) or generation throws.
- Feature roadmaps record delivered implementation only; production sign-off lives in
  `release-roadmap-V{n}`.

## 9.3 To capture (tech-leader)
- [ ] The current authoritative roadmap state (V*N* onward) and the next gate.
- [ ] Which of the phases above become `roadmap-vN` entries, and in what order.
- [ ] The dependency graph (e.g. client foundation → interiors → control).

---
*Co-author invitation: @tech-leader — align this with the board and the roadmap index.*
