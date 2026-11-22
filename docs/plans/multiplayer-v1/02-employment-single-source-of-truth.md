# Step 02 — Employment single source of truth

**Depends:** 01
**Goal:** Prevent `EmploymentIndex` from drifting from ECS state over a long-lived world.

**Status:** Done

## Context

`EmploymentIndex` is a mutable dictionary outside the ECS. Company deletion currently
leaves `Employee` components and index entries behind.

## Tasks

- [x] Decide ownership: either derive counts from `Employee` queries, or keep the index and make it authoritative-maintained.
- [x] Recommended: keep the index, but route all mutation through one owner (HiringSystem + a delete path).
- [x] Implement `EmploymentIndex.RemoveByCompany(Entity company)`.
- [x] On company delete: remove/destroy employee links and clear index entries atomically.
- [x] On employee destroy/removal: remove from index.
- [x] Add an invariant check (debug-only) comparing index vs ECS counts.
- [x] Add tests for hire → delete company → count = 0.

## Files likely touched

- `Genelife/Components/Employment/EmploymentIndex.cs`
- `Genelife/Systems/HiringSystem.cs`
- `Genelife.Api/Repositories/CompanyRepository.cs`

## Acceptance

- No ghost employees after company deletion.
- Employee counts match ECS truth after any sequence of hire/delete.

## Testing

- Property-style sequence test: hire N, delete company, assert zero.
- Invariant test toggled in CI.
