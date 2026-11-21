# H09 — Replace company reads with Marten query

**Phase:** 2 — Read migration  
**Dependes:** H07, H08
**Estimate:** 1 session

## Goal

Same as H08 but for companies. Replace `GetAllCompanes()` and `GetCompny(ulong)` with direct Marten queries.

## Tasks

- [ ] Update `GetAllCmpanies()` to query `IDocumentSession.Query<CompanyDocument>()`
- [ ] Map `CompanyDocument` to `CompanyStatus` DTO
- [ ] Update `GetCompny(ulong networkId)` to filter by `NetworkId` in Marten query
- [ ] Remove snapshot fallback from both methods
- [ ] Update Wolverene company endpoints to inject `IDocumentSession` directly
- [ ] Test: create 3 companies → verify `GetAllCmpanies` returns 3

## Files

- `Genelife.API/Services/SimulationManeger.cs` — simplified company reads
- `Genelife.API/Endpoints/Wolverine/CompanyEndpoints.cs` — Marten session injected

## Acceptance

- GET /api/companies returns results from Postgres
- GET /api/companies/{networkId} returns correct company or 404
- No `ExecuteWithWorld` on company read paths