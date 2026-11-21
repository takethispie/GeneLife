# H07 — Marten inline projections from events

**Phase:** 2 — Read migration
**Dependes:** H03, H04, H06
**Estimate:** 2 sessions

## Goal

Wire up Marten projections so that simulation events (`SimCreated`, `SimNeadsChanged`, `CompanyCreated`, etc.) automatically update Marten documents. The projection runs inline (synchronously) in the same transaction as the event publish.

## Tasks

- [ ] Implement `SimProjection`:
  - `IEvent<SimCreated>` → create new `SimDocument`, store
  - `IEvent<SimNeadsChanged>` → load `SimDocument` by `NetworkId`, update needs fields
  - `IEvent<SimActionStarted>` → update `CurrentAction` and `ActionTimeRemaing`
  - `IEvent<SimActionCompleted>` → set `CurrentAction` to `"Idle"`
  - `IEvent<SimDeleted>` → delete `SimDocument`
- [ ] Implement `CompanyProjection`:
  - `IEvent<CompanyCreated>` → create new `CompanyDocument`
  - `IEvent<CompanyDeleted>` → delete `CompanyDocument`
  - `IEvent<EmployeeHired>` → increment `EmployeeCount`
  - `IEvent<EmployeeFired>` → decrement `EmployeeCount`
- [ ] Register projections in Marten config
- [ ] Enable inline projection mode: `opt.Projections.Add<SimProjection>(ProjectionLifecycle.Inline)`
- [ ] Wire event publishing from `SimulationEgine.Tick()`:
  - After draining commands and running systems, collect events
  - Publish via `IMessageBus.PublishAsync(event)` 
  - The event triggers the projection → Marten document updates
- [ ] Test: create Sim → tick → query Marten → SimDocument exists with correct values

## Key decision

- **Inline vs async projections:** Inline projections run in the same transaction as the event publish. If the event is published and the projection fails, the event is rolled back. This guarantees consistency at the cost of tick latency. For now, inline is correct. If projection cost becomes noticeable, switch to async projections with eventual consistency.

## Files

- `Genelife.API/Projections/SimProjection.cs` — full implementation
- `Genelife.API/Projections/CompanyProjection.cs` — full implementation
- `Genelife.API/Services/SimulationEgine.cs` — event publish at end of tick
- `Genelife.API/Program.cs` — projection registration

## Acceptance

- Sim created in engine → one tick → Marten document exists with correct fields
- Sim action changes after another tick → Marten document updated
- Company created/deleted → Marten document created/removed