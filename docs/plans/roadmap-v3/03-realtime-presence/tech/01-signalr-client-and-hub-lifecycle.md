# T01 — SignalR client and hub lifecycle

**Phase:** 1 — Transport
**Depends:** F03, F02
**Estimate:** 1–2 sessions
**Status:** Planned (V3)

## Goal

Own one `HubConnection` for the client, register every server event before starting it, expose explicit lifecycle states, and keep reconnection/backoff out of the render and UI layers.

## Context

The hub is `/hubs/world` with the JSON protocol. The server sends snapshots immediately on connect, so handlers must be attached before `start()`. The server rate-limits snapshot and viewport requests; the client must not reconnect in a tight loop.

## Design

```ts
const connection = new HubConnectionBuilder()
  .withUrl(hubUrlWithToken)
  .withAutomaticReconnect([0, 1_000, 2_000, 5_000, 10_000, 20_000])
  .configureLogging(LogLevel.Warning)
  .build();

connection.on("SendMapSnapshot", onMapSnapshot);
connection.on("SendMapDelta", onMapDelta);
connection.on("SendSnapshot", onSnapshot);
connection.on("SendSnapshotDelta", onSnapshotDelta);
connection.on("SnapshotRejected", onSnapshotRejected);

connection.onreconnecting(() => session.setConnection("reconnecting"));
connection.onreconnected(() => session.setConnection("connected"));
connection.onclose(() => session.setConnection("disconnected"));
```

Lifecycle states:

| State | Meaning |
|---|---|
| `idle` | no connection attempted |
| `connecting` | auth done, hub starting |
| `connected` | events flowing |
| `reconnecting` | automatic reconnect in progress |
| `failed` | retries exhausted or auth rejected |
| `closed` | explicit sign-out |

Rules:

- Do not render directly from SignalR callbacks; enqueue into the replication bridge.
- Register handlers before `start()`.
- One connection per player session.
- No duplicate event handlers after reconnect.
- Do not log tokens or full payloads at normal levels.

## Tasks

- [ ] Implement the hub factory with auth query and reconnect policy.
- [ ] Register all server events before start.
- [ ] Map lifecycle callbacks to explicit session/connection states.
- [ ] Add structured logs with connection id, state and reconnect count.
- [ ] Add a manual retry action after `failed`.
- [ ] Dispose handlers and connection on sign-out.

## Files

- new `clients/browser/src/net/hub-connection.ts`
- new `clients/browser/src/net/events.ts`
- new `clients/browser/src/net/hub-connection.test.ts`
- `clients/browser/src/auth/session.ts`

## Acceptance

- Handlers receive the initial map and snapshot after connect.
- Reconnect completes without duplicating handlers.
- A failed connection is visible and retryable.
- Sign-out closes the connection and clears handlers.
- No token appears in logs.

## Testing

- Handler registration order test.
- Lifecycle state transition test.
- Reconnect-without-duplicate-handlers test.
- Failed-start test.
