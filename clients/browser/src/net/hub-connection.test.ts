import { describe, expect, it } from "vitest";
import type { HubHandlers } from "./events";
import { buildHubUrl, HubNotConnectedError, WorldHubClient } from "./hub-connection";

const handlers: HubHandlers = {
  onWelcome: () => undefined,
  onMapSnapshot: () => undefined,
  onMapDelta: () => undefined,
  onSnapshot: () => undefined,
  onSnapshotDelta: () => undefined,
  onSnapshotRejected: () => undefined,
  onIntentRejected: () => undefined,
  onStateChange: () => undefined
};

describe("buildHubUrl", () => {
  it("adds the access token without dropping existing query parameters", () => {
    const url = new URL(buildHubUrl("https://example.test/hubs/world?mode=play", "a b+c"));
    expect(url.pathname).toBe("/hubs/world");
    expect(url.searchParams.get("mode")).toBe("play");
    expect(url.searchParams.get("access_token")).toBe("a b+c");
  });
});

describe("WorldHubClient", () => {
  it("fails closed before a connection exists", async () => {
    const client = new WorldHubClient("https://example.test/hubs/world", handlers);
    expect(client.currentState).toBe("idle");
    await expect(client.requestSnapshot(null)).rejects.toBeInstanceOf(HubNotConnectedError);
    await expect(client.setViewport({ centerX: 0, centerY: 0, radius: 8 })).rejects.toBeInstanceOf(
      HubNotConnectedError
    );
    await expect(
      client.submitIntent({
        type: "move",
        playerId: "p",
        sequence: 1,
        genosNetworkId: 1,
        targetTileX: 1,
        targetTileY: 1
      })
    ).rejects.toBeInstanceOf(HubNotConnectedError);
  });
});
