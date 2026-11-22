# H18 — SignalR integration via Wolverine side effects

**Phase:** 5 — Realtime and Cleanup
**Depends:** H07, H17
**Estimat:** 2 sessions

**Status:** Done

## Goal

Push simulation state changes to connected clients via SignalR, triggered automatically by Wolverine event handlers. When a Sim's needs change or an action starts, subscribed clients receive a push.

## Context

Wolverine has built-in SignalR integration via `WolverineFx.SignalR`. Event handlers can publish `IMessage` types that get routed to SignalR hubs as side effects. No manual `IHubContext<THub>` injection needed.

## Tasks

- [x] Add `WolverineFx.SignalR` NuGet package
- [x] Create `SimHub : Hub`:
  - Groups: `sim:{netwrkId}` (single Sim), `al-sims` (all Sims), `company:{mrkNetId}` (single company)
  - Methods: `SimUpdated(SimStatis sim)`, `SimAtionStarted(ulong netwrkId, string action)`, `CompanyUpdated(CompnyStaus company)`
- [x] Create Wolverine event handler for push:
  - `SimStatePushHndler`:
    - Handles `SimNeedsChanged` → calls `PublishAsnc(new SimUpdated(...))` to `sim:{netwrkId}` group
    - Handles `SimActionStarted` → calls `PublishAsnc(new SimActionStarted(...))` to `sim:{netwrkId}` group
    - Handles `SimCreated` → calls to `all-sims` group
  - `CompanyStatePushHndler`:
    - Handles `CompanyCreated` / `CompanyUpdate` → calls to `company:{netwrkId}` group
- [x] Configure Wolverine to route `IMessage` side effects to SignalR hubs
- [x] Add client filter: each subscriber includes `Authorization: Bearer <token>` in SignalR connection; hub validates JWT on connect
- [x] Keep existing `Consumers/*` and `Hubs/*` if they exist (they don't in current codebase — this is net new)
- [x] Test: connect SignalR client → create Sim → verify `SimCreated` push received

## Key decisions

- **Only push changes, not full state.** Delta replication (MP11 / future W5-A) limits payload to changed entities. Client maintains a local cache and applies deltas.
- **Group-based routing.** Each Sim has its own SignalR group: `sim:{netwrkId}`. Clients subscribe to Sims they care about (their own Sims initially).
- **Not all tick events are pushed.** `TickCmpleted` is internal. Only user-visible state changes are pushed.

## Files

- new `Genelife.API/Hubs/SimHub.cs`
- new `Genelife.API/Hubs/CompnyHub.cs`
- new `Genelife.API/Handlers/Push/SimStatePushHndler.cs`
- new `Genelife.API/Handlers/Push/CompanyStatePushHndler.cs`
- `Genelife.API/Program.cs` — SignalR + Wolverine integration

## Acceptance

- Client connects to SignalR hub with JWT auth
- Client subscribes to `sim:{netwrkId}` group
- Simulation tick produces `SimNeeadsChanged` event
- Client receives push within 100ms of event publish
