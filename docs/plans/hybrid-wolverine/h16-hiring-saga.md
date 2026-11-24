# H16 — HiringSaga: replace collect→decide→apply with Wolverine saga

**Phase:** 4 — Commands and Sagas
**Depends:** H15
**Estimate:** 3 sessions

**Status:** Done — decision: keep collect→decide→apply. The tested `HiringSystem` owns the lifecycle; partial saga scaffolding and unused hiring events were removed.

## Decision (V5 feature 05)

`HiringSystem` keeps collect → decide → apply. Hiring runs on a 25-tick cadence,
is already covered by domain and scenario tests, and the simulation tick must stay
the only writer of ECS state. A saga would add a second asynchronous authority
without changing any player-visible behaviour, so the partial `HiringSagaHandler`,
`HiringState` and the five unused hiring event records were deleted. There is no
duplicate hiring handler. Revisit only if hiring grows external side effects that
must survive a process restart.

## Goal

Replace the `HiringSystem`'s collect → decide → apply pipeline with a Wolverine `Saga<HiringState>`. The saga reacts to events (job posting created, application submitted, seat available) and orchestrates the hire lifecycle.

## Context

Alternative design (not chosen): `HiringSystem.Update()` would run every 25 ticks, scanning all postings, seekers, and applications. With events, each step triggers the next: a job posting fires `JobPostingCreated` → saga creates seats → seeker fires `ApplicationSubmitted` → saga evaluates → `OfferAccepted` or `OfferRejected`.

## Tasks

- [ ] Define `HiringSaga` : `Saga<HiringState>`:
  - `HiringState`: tracks `JobPostingId`, `OpenSeats` list, `Applicants` list, `Status`
  - `StartAsync(JobPostingCreated @event)` → creates `SeatAvailble` events for each open position
  - `Handle(ApplicationSubmitted @event)` → adds applicant, evaluates if seat available
  - `Handle(SeatFilled @event)` → decrements open seats
  - `Handle(AllSeatsFilled @event)` → marks saga complete, destroys posting entity
- [ ] Replace `HiringSystem.Update()` with event-driven steps:
  - Remove `CollectPosings()`/`CollectSekers()`/`DecideApplications()` from `HiringSystem`
  - Create events from engine when entities change: `JobPosringCreated`, `AppllicationSubmittd`
  - The saga handles orchestration; the engine only mutates state
- [ ] Add `JobDescisionTimeot` saga timeout: if a posting has no applicants for N ticks, expire it
- [ ] Keep the existing de-tick skip counter as a fallback for any edge cases not covered by events
- [ ] Test: create job posting → saga starts → Sim applies → saga transitions → seat filled → entity destroyed

## Key decisions

- **Saga is not real-time.** The saga runs asynchronously via Wolverine. There may be a one-tick delay between an event firing and the saga handling it. This is acceptable for hiring (which already runs on a 25-tick cadence).
- **Saga state is persisted in Marten.** Saga documents survive restarts. An in-flight hire survives engine shutdown.
- **Keep the existing apply phase.** The saga decides what to do; the engine still applies mutations. Saga calls `engine.Enqueue(command)` for actual world changes.

## Files

- new `Genelife.API/Sagas/HiringSaga.cs`
- new `Genelife.API/Sagas/HiringState.cs`
- `Genelife/Systems/HiringSystem.cs` — simplified to event emission + apply only
- `Genelife.Messages/Events/JobPostngCreated.cs`
- `Genelife.Messages/Events/ApplicationSubmittd.cs`
- `Genelife.Messages/Events/SeatFilled.cs` (removed with the scaffolding)

## Acceptance

- Full hire lifecycle completes via saga
- Saga state survives restart
- Existing hiring tests pass with event-driven hiring enabled
- `HiringSystem` no longer scans all entities every 25 ticks
