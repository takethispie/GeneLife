# Parallel Independent Systems

## Context

`SystemRegistry.Update` runs systems serially on the tick thread. Arch supports chunk iteration, and some systems read/write disjoint components. Parallelizing those is the biggest potential win at 2,000+ genoses, but it carries the most determinism risk.

## Design

- Identify systems with disjoint read/write sets.
- Run those systems in parallel across Arch chunks.
- Keep a fixed reduction order so results are deterministic.
- Keep non-parallelizable systems serial.

Do not parallelize anything that creates, adds, removes or destroys entities, or that shares write access to the same component.

## Tasks

- [ ] Classify systems by read/write set.
- [ ] Parallelize the safe systems with fixed reduction order.
- [ ] Prove determinism on the replay suite for NPC-only and 32-player worlds.
- [ ] Run the 2k decade and record p95/p99 and allocation.

## Files

- `Genelife.Application/Systems/SystemRegistry.cs`
- Affected systems in `Genelife.Application/Systems`

## Contracts

- Simulation outcomes are identical to the serial path.
- Entity ordering and reduction order are stable.

## Acceptance

- Parallel systems produce identical results to the serial path on the replay suite.
- The 2k decade stays inside the tick budget with no dropped ticks.
- Determinism and replay tests pass for NPC-only and 32-player worlds.
