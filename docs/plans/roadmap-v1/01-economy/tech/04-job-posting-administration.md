# E04 — Job posting administration

**Phase:** 2 — Labour market
**Depends:** E03
**Estimate:** 2 sessions
**Status:** Planned

## Goal

Expose the existing posting/seat model to operators and enforce its invariants. A posting is the only way a company can hire.

## Context

`JobPosting` (`Genelife/Components/Employment/JobPosting.cs`) stores company, office, position, salary and a `Skill` bitmask. Each seat is an `OpenPosition` entity. `HiringSystem` consumes both and destroys filled seats and fully filled postings (`HiringSystem.cs:47`). There is a `CreateJobPosting` message (`Genelife.Messages/Commands/CreateJobPosting.cs`) but no handler or endpoint. Salary is `float` today and becomes `Money` in E01.

## Design

`JobPosting` gains explicit seat count and posting state:

```csharp
public record struct JobPosting
{
    public Entity Company;
    public Entity Office;
    public string Position;
    public Money SalaryPerCycle;
    public Skill Requirements;
    public int OpenSeats;
    public long PostedAtTick;
    public bool IsClosed;
}
```

Seat entities remain the source of truth for open capacity; `OpenSeats` is a cached count maintained together with seat creation/destruction. `JobPostingRepository`:

- `Open(...)` validates company, office ownership, office not closed, capacity available;
- creates one `OpenPosition` entity per seat in a single apply phase;
- `UpdateSalary` rejects changes below the company's minimum band;
- `Close` destroys remaining seats, then the posting, and cancels pending applications with `ApplicationStatus.Rejected`;
- `CloseAllForCompany` is used by bankruptcy and office closure.

Salary is per pay cycle (quarter). Positions are free text but a canonical list is seeded for the admin UI: `Associate`, `Specialist`, `Senior`, `Lead`, `Manager`, `Director`.

## Tasks

- [ ] Extend `JobPosting`; update `HiringSystem` and all constructors/tests.
- [ ] Add `JobPostingRepository` with open/update/close and invariant checks.
- [ ] Replace the untyped `CreateJobPosting` message with typed company, office, position, salary, requirements, seats.
- [ ] Add Wolverine endpoints `POST/PATCH/DELETE /api/job-postings`.
- [ ] Emit `JobPostingOpened`, `JobPostingClosed`, `JobSeatReserved`, `JobSeatReleased`.
- [ ] Add `JobPostingDocument` with open seat count, company and office names.
- [ ] Add `JobPostingStatus` fields: id, company id, office id, salary, seats, age in ticks, state.
- [ ] Reject postings that exceed remaining office capacity with `409 Conflict`.

## Files

- `Genelife/Components/Employment/JobPosting.cs`
- new `Genelife.API/Repositories/JobPostingRepository.cs`
- `Genelife.Messages/Commands/CreateJobPosting.cs`
- `Genelife.Messages/Events/*`
- `Genelife.API/Endpoints/Wolverine/JobPostingEndpoints.cs`
- `Genelife.API/Models/JobPostingDocument.cs`
- `Genelife.API/Services/SimulationEngine.cs`

## Contracts

| Method | Route | Semantics |
|---|---|---|
| POST | `/api/job-postings` | create posting + N seats atomically |
| GET | `/api/job-postings` | list with seat counts and state |
| PATCH | `/api/job-postings/{id}` | salary, seat count (grow/shrink), close flag |
| DELETE | `/api/job-postings/{id}` | close and reject pending applications |

## Acceptance

- Creating a posting with 5 seats produces exactly 5 `OpenPosition` entities.
- Filling all seats removes the posting, matching current `HiringSystem` behaviour.
- Shrinking seats below filled seats is rejected.
- Closing a posting rejects pending applications and frees capacity.
- Admin cannot post jobs for a closed office or another company's office.

## Testing

- Repository invariant tests.
- Integration test: open → apply → hire → posting disappears.
- Capacity conflict test.
- Projection seat-count test.
