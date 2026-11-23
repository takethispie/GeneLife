# F01 — Pregnancy domain model

**Phase:** 1 — Foundation
**Depends:** features 01 and 02
**Estimate:** 1 session
**Status:** Planned (V4)

## Goal

Represent an active or historical pregnancy with its own network id, parents, timing, risk and outcome so conception, progression, birth, loss and the admin can all reference the same fact.

## Context

A pregnancy is not a permanent property of a sim and it has a lifecycle with history. A component on the carrying sim would make loss history, multiple pregnancies over time and admin queries awkward. An entity with a `Pregnancy` component fits the existing edge/entity patterns.

## Design

### Status

```csharp
public enum PregnancyStatus : byte
{
    Active,
    ReadyToBirth,
    Completed,
    Lost,
    Cancelled
}
```

### Source

```csharp
public enum PregnancySource : byte
{
    Conception,
    Admin,
    Migration
}
```

### Component

```csharp
public record struct Pregnancy
{
    public Entity CarryingParent;
    public Entity OtherParent;
    public long ConceivedTick;
    public long DueTick;
    public int RiskBasisPoints;
    public int HealthAtConception;
    public PregnancyStatus Status;
    public PregnancySource Source;
    public long LastProgressTick;
    public int ChildrenExpected;
    public int Complications;
    public Money CarePaid;
}
```

### Rules

- One active `Active`/`ReadyToBirth` pregnancy per carrying parent.
- `DueTick > ConceivedTick`; `ChildrenExpected` defaults to 1.
- `CarryingParent` and `OtherParent` are alive, adult and partnered at conception.
- The pregnancy entity has a `NetworkId` for persistence and the admin.
- A completed or lost pregnancy remains as history; it is not destroyed immediately.
- Risk is stored in basis points so it is deterministic and displayable.
- No pregnancy is created for a minor or a dependent.

### Index

`PregnancyIndex`:

- `ActiveByCarryingParent: Dictionary<Entity, Entity>`
- `Active: List<Entity>`
- `HistoryByParent: Dictionary<Entity, List<Entity>>`

Rebuilt on restore.

## Tasks

- [ ] Add `PregnancyStatus`, `PregnancySource` and `Pregnancy`.
- [ ] Add `PregnancyIndex` and rebuild/update methods.
- [ ] Add `PregnancyService.Start`, `Progress`, `Complete`, `Lose` and `Cancel`.
- [ ] Add one-active-pregnancy validation.
- [ ] Add a test fixture with a known conception/due tick.
- [ ] Document that history is retained.

## Files

- new `Genelife/Components/Family/Pregnancy.cs`
- new `Genelife/Family/PregnancyIndex.cs`
- new `Genelife/Family/PregnancyService.cs`
- `Genelife.UnitTests/Family/PregnancyDomainTests.cs`

## Acceptance

- A pregnancy has a unique network id and valid parent/tick fields.
- A second active pregnancy for the same carrying parent is rejected.
- A minor or dependent carrying parent is rejected.
- `Complete`, `Lose` and `Cancel` set the status and keep history.
- The index reflects active and historical pregnancies after rebuild.

## Testing

- Start and complete a pregnancy.
- Attempt a duplicate active pregnancy.
- Rebuild the index and compare lookups.
- Assert history survives a status change.