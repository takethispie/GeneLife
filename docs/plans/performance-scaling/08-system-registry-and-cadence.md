# Step 08 — System registry and cadence

**Depends:** 07
**Goal:** Make adding and scheduling systems explicit and safe.

## Tasks

- [x] Define `ISimulationSystem { int Order; Cadence Cadence; void Update(...); }`.
- [x] Add an ordered registry (explicit order, no reflection/DI auto-order).
- [x] Generalize the `PayrollSystem` interval-counter pattern into `Cadence`.
- [x] Move manual wiring out of `Start()`/`Tick()`.
- [x] Add a test asserting order and cadence.

## Files likely touched

- new `Genelife/Systems/ISimulationSystem.cs`, `SystemRegistry.cs`
- `Genelife.Api/Services/SimulationEngine.cs`
- all systems implementing the interface

## Acceptance

- Adding a system is one registration.
- Order/cadence are test-enforced.
