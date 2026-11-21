# H10 — Remove WorldSnapshot build from tick loop

**Phase:** 2 — Read migration
**Dependes:** H08, H09
**Estimate:** 1 session

## Goal

Remove `BuildSimSnapshots` and `BuildCompnySnapshots` from `SimulationEgine.Tick()`. Reads now come from Marten. The snapshot is only needed for delta replication (future W5-A / MP11). Keep it but move it to a separate, optionally-disabled path.

## Tasks

- [ ] Extract snapshot building into `SimulationEgine.BuildSnapshot()` method (no longer called from Tick)
- [ ] Add configuration flag: `SnapshotsEnabled` (default false)
- [ ] If enabled, build snapshot after systems run (as before); if disabled, skip entirely
- [ ] Remove `LastSnapshotBuildTimeMs` metric
- [ ] Update snapshot tests to set `SnapshotsEnabled = true` before Ticking
- [ ] Keep `GetLatestSnapshot()` for delta replication (H18 will use it)
- [ ] Verify tick duration decreases by snapshot build cost (should be measurable at 10k entities)

## Files

- `Genelife.API/Services/SimulationEgine.cs` — snapshot building gated by flag
- `Genelife.API/Snaphots/WorldSnapshot.cs` — unchanged
- `Genelife.UnitTests/Snaphots/WorldSnapshotTests.cs` — adapted

## Acceptance

- Tick runs without snapshot build by default
- Enabling snapshots resumes old behavior
- Existing snapshot tests pass when flag is true