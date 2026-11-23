# I03 — Building interaction and feedback

**Phase:** 3 — Interaction
**Depends:** I02, R03
**Estimate:** 1–2 sessions
**Status:** Planned (V3)

## Goal

Let the player select a building, understand its type and entry, submit `UseBuildingIntent`, and receive honest success or range/role feedback.

## Context

`UseBuildingIntent` carries `SimNetworkId` and `BuildingNetworkId`. The server checks ownership, building existence, entry distance (`ChebyshevDistance <= 1`) and role (office employee, school, residence). The client cannot assert any of that.

## Design

### Selection

- Clicking a building selects it and shows a details panel.
- Display name, kind, district, footprint, entry and owner if present.
- Highlight the footprint and entry marker.
- Keep selection across snapshots; clear it if the building is removed by a map delta.

### Interaction

- Show an “Interact” action when the local sim is near the entry.
- The “near” calculation is a UX hint only; the server is authoritative.
- Submit:

```json
{
  "type": "useBuilding",
  "playerId": "player-1",
  "sequence": 43,
  "simNetworkId": 12345,
  "buildingNetworkId": 67890
}
```

- On `IntentResult.success`, show “Requested” and wait for the resulting action/snapshot.
- On `error`, show the reason.
- If the server silently rejects (pre-`IntentRejected`), time out to “Not accepted” and keep the previous state.

### Feedback

| Case | UI |
|---|---|
| Out of range | “Walk to the entrance first” |
| Wrong role | “You cannot use this building” |
| Building removed | clear selection, “Building no longer exists” |
| Accepted | “Requested” then reflect the action |
| Unknown rejection | “Action not accepted” |

## Tasks

- [ ] Implement building selection and details panel.
- [ ] Render footprint and entry highlight.
- [ ] Implement the `UseBuildingIntent` builder.
- [ ] Add near-entry hint and server-authoritative submit.
- [ ] Handle removal and rejection.
- [ ] Add tests for payload, removal and feedback cases.

## Files

- new `clients/browser/src/input/building-selection.ts`
- new `clients/browser/src/net/intent-client.ts`
- new `clients/browser/src/ui/building-panel.ts`
- new `clients/browser/src/input/building-selection.test.ts`

## Acceptance

- A building can be selected and inspected.
- Selection clears when the building is removed.
- The interaction payload contains sim and building ids, not positions.
- Range/role failures are visible, not faked.
- A successful request is reflected only after server state changes.
- The client never claims an action the server did not accept.

## Testing

- Selection/details test.
- Payload test.
- Removal test.
- Range/role feedback test.
- Accepted-request test.
