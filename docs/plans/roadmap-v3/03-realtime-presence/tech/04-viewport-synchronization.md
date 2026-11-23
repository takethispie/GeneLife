# T04 — Viewport synchronization

**Phase:** 4 — Interest
**Depends:** R04, T01
**Estimate:** 1–2 sessions
**Status:** Planned (V3)

## Goal

Turn the camera into a server viewport without flooding the hub, respect the server’s clamped result, and handle the fresh snapshot that follows a viewport change.

## Context

`SetViewport(SetViewportRequest)` returns `ViewportResult` and invalidates the next delta. The server rate-limits viewport changes and clamps center/radius to map bounds and configured limits. A client that assumes delta continuity after a viewport change will miss stationary entrants.

## Design

```ts
type ViewportRequest = { centerX: number; centerY: number; radius: number };
type ViewportResult = {
  success: boolean;
  centerX: number;
  centerY: number;
  radius: number;
  error?: string | null;
};
```

Flow:

1. Camera computes visible tile rect.
2. Convert to center/radius.
3. Debounce 200 ms and skip sub-threshold changes.
4. Call `SetViewport`.
5. If `success`, store the returned center/radius as effective.
6. Expect a full filtered `SendSnapshot`; do not assume a delta from the old viewport.
7. If `success` is false, show the error and keep the previous effective viewport.

Rules:

- Never call `SetViewport` per frame.
- Never call it below the server’s minimum or above its maximum.
- Do not call `ClearViewport` unless the player explicitly opts out.
- Re-sync after reconnect or map change.
- If the map is replaced, rebuild the camera bounds and re-request a viewport.

## Tasks

- [ ] Implement visible-rect to viewport conversion.
- [ ] Implement debounce and change threshold.
- [ ] Implement `SetViewport` invocation and result handling.
- [ ] Handle the post-change full snapshot.
- [ ] Re-sync on reconnect and map replacement.
- [ ] Add debug metrics: requests, clamped results, errors.
- [ ] Add a “no filtering” developer toggle only if needed.

## Files

- new `clients/browser/src/net/viewport-client.ts`
- new `clients/browser/src/render/camera.ts`
- new `clients/browser/src/simulation/store.ts`
- new `clients/browser/src/net/viewport-client.test.ts`

## Acceptance

- Pan/zoom produces at most one viewport request per debounce window.
- The effective viewport equals `ViewportResult`, not the raw request.
- A viewport change triggers a full filtered snapshot.
- Reconnect restores or re-derives the viewport.
- Map replacement re-syncs the viewport.
- Failures are visible and do not desynchronise the store.

## Testing

- Debounce/threshold tests.
- Clamp-result tests.
- Post-change full snapshot test.
- Reconnect and map-change tests.
- Rate-limit avoidance test.
