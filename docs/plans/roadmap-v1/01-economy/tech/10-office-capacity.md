# E10 — Office capacity and utilisation

**Phase:** 5 — Labour market economics
**Depends:** E03, E04, E09
**Estimate:** 1 session
**Status:** Planned

## Goal

Make office capacity a hard constraint that shapes company growth and prevents hidden overstaffing.

## Context

`Office.Capacity` exists but nothing enforces it. `HiringSystem` hires against seats, and seats are created by postings without checking capacity. A company could post 500 seats into a 10-person office.

## Design

Seat accounting per office:

```
used = employees assigned to office + open seats for postings tied to office
available = capacity − used
```

Rules:

- Opening a posting requires `seats <= available`.
- Growing a posting requires the additional seats to be available.
- A hire consumes an open seat, so `used` is unchanged at hire time.
- A separation frees an employee slot; if the posting still exists, a seat is recreated.
- `Office.SeatsUsed` is a cache updated only in apply phases. Debug builds assert it equals the computed value.
- Utilisation = `employees / capacity`. Reservation rate = `openSeats / capacity`.

When a company is at capacity and healthy, a new office is opened only through an explicit admin action. V1 does not silently create offices.

## Tasks

- [ ] Add `OfficeSeatAccounting` service with pure calculation methods.
- [ ] Enforce capacity in `JobPostingRepository.Open` and `UpdateSeats`.
- [ ] Update `SeatsUsed` on hire, separation, posting open/close and posting resize.
- [ ] Add debug invariant checks comparing cached and computed values.
- [ ] Add capacity fields to `OfficeStatus`: capacity, employees, open seats, utilisation.
- [ ] Reject hires that would exceed capacity with a clear event and metric.
- [ ] Add an admin warning when utilisation is above 95% or reservations are stale.

## Files

- new `Genelife.API/Services/OfficeSeatAccounting.cs`
- `Genelife.API/Repositories/JobPostingRepository.cs`
- `Genelife/Systems/HiringSystem.cs`
- `Genelife/Components/Company/Office.cs`
- `Genelife.API/DTOs/OfficeStatus.cs`

## Acceptance

- A posting cannot reserve more seats than an office has available.
- Hiring never exceeds capacity.
- Closing a posting releases its seats immediately.
- `OfficeStatus` reports correct capacity, employees, open seats and utilisation.
- Debug invariant passes after a mixed hire/close/separate sequence.

## Testing

- Capacity boundary tests (exact fit, one over).
- Multi-office company test.
- Cached vs computed seats test.
- Admin read-model test.
