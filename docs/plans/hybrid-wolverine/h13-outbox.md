# H13 — Outbox pattern for reliable event publishing

**Phase:** 3 — Persistence
**Depends:** H07, H12
**Estimate:** 1 session

## Goal

Ensure events published from the engine are durable. If the process crashes between publishing an event and Marten committing it, the event is lost. Wolverine's transactional outbox solves this.

## Context

Wolverine + Marten provide a built-in outbox. When you call `IMessageBus.PublishAsync(event)` inside a Marten `IDocumentSession` scope, Wolverine enqueues the event in the same Postgres transaction as the document updates. On commit, both the document changes and the outgoing messages are persisted atomically.

## Tasks

- [ ] Wrap event publishing in the existing Marten session scope:
  - Create an `IDocumentSession` at the start of each tick
  - Run the tick (systems, command drain)
  - Build events list
  - Call `bus.PublishAsync(event)` for each event (this writes to the outbox)
  - Call `session.SaveChangesAsync()` (commits both documents and outbox messages)
- [ ] Configure Wolverine to use durable outbox: `opts.Durability.Mode = DurabilityMode.Durable`
- [ ] Add periodic outbox processing: Wolverine's background agent polls the outbox table and sends pending messages
- [ ] Test: publish event during tick → verify outbox table has a row → verify handler receives the event after commit

## Key decision

- **One session per tick.** Creating a new `IDocumentSession` per tick keeps the transaction scope small (one tick's worth of events and document changes). If a tick fails, only that tick's work is rolled back.

## Files

- `Genelife.API/Services/SimulationEngine.cs` — session scoping and outbox publish
- `Genelife.API/Program.cs` — outbox configuration

## Acceptance

- Outbox table (`wolverine_outgoing_envelopes`) has rows after each tick
- Messages are delivered to handlers after transaction commit
- Simulated crash mid-tick: events from that tick are NOT delivered (rollback) — but previous ticks' events are safe