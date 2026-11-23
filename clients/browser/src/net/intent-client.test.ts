import { describe, expect, it, vi } from "vitest";
import type { Intent } from "../contracts/hub";
import { IntentClient, MemorySequenceStore } from "./intent-client";

const base = {
  playerId: () => "player-1",
  ownSimNetworkId: () => 7
};

describe("IntentClient", () => {
  it("sends tile-based move intents with monotonic sequences", async () => {
    const sent: Intent[] = [];
    const store = new MemorySequenceStore();
    const client = new IntentClient({
      ...base,
      sequenceStore: store,
      minIntervalMs: 0,
      invoke: async (intent) => {
        sent.push(intent);
        return { success: true };
      }
    });

    await client.moveTo(3, 4);
    await client.moveTo(5, 6);

    expect(sent[0]).toMatchObject({
      type: "move",
      playerId: "player-1",
      simNetworkId: 7,
      targetTileX: 3,
      targetTileY: 4,
      sequence: 1
    });
    expect(sent[1]).toMatchObject({ targetTileX: 5, targetTileY: 6, sequence: 2 });
    expect(client.lastSequence).toBe(2);
    expect(store.load()).toBe(2);
    expect("x" in sent[0]!).toBe(false);
    expect("y" in sent[0]!).toBe(false);
  });

  it("keeps sequences monotonic across instances using persisted values", async () => {
    const store = new MemorySequenceStore();
    store.save(41);
    const client = new IntentClient({
      ...base,
      sequenceStore: store,
      invoke: async () => ({ success: true })
    });
    await client.moveTo(1, 1);
    expect(client.lastSequence).toBe(42);
  });

  it("sends useBuilding intents", async () => {
    const invoke = vi.fn(async () => ({ success: true }));
    const client = new IntentClient({ ...base, invoke });
    await client.useBuilding(7002);
    expect(invoke).toHaveBeenCalledWith(
      expect.objectContaining({
        type: "useBuilding",
        buildingNetworkId: 7002,
        simNetworkId: 7
      })
    );
  });

  it("rate limits locally without queueing", async () => {
    let now = 1_000;
    const invoke = vi.fn(async () => ({ success: true }));
    const client = new IntentClient({ ...base, invoke, now: () => now, minIntervalMs: 90 });

    expect((await client.moveTo(1, 1)).status).toBe("sent");
    now += 10;
    const second = await client.moveTo(2, 2);
    expect(second.status).toBe("rate-limited");
    expect(invoke).toHaveBeenCalledTimes(1);
  });

  it("fails closed when the sim is not ready", async () => {
    const invoke = vi.fn(async () => ({ success: true }));
    const client = new IntentClient({ ...base, ownSimNetworkId: () => 0, invoke });
    const outcome = await client.moveTo(1, 1);
    expect(outcome.status).toBe("not-ready");
    expect(invoke).not.toHaveBeenCalled();
  });

  it("reports admission failures", async () => {
    const client = new IntentClient({
      ...base,
      invoke: async () => ({ success: false, error: "Payload must be a JSON object" })
    });
    const outcome = await client.moveTo(1, 1);
    expect(outcome).toEqual({ status: "failed", message: "Payload must be a JSON object" });
  });

  it("matches asynchronous rejections to the pending intent", async () => {
    const onRejected = vi.fn();
    let sentIntent: Intent | null = null;
    const client = new IntentClient({
      ...base,
      sequenceStore: new MemorySequenceStore(),
      invoke: async (intent) => {
        sentIntent = intent;
        return { success: true };
      },
      onRejected
    });

    await client.moveTo(9, 9);
    client.handleRejected(1, "blocked", "That tile is blocked");
    expect(onRejected).toHaveBeenCalledWith(
      "blocked",
      "That tile is blocked",
      expect.objectContaining({ type: "move", targetTileX: 9 })
    );
    expect(sentIntent).not.toBeNull();
  });

  it("surfaces transport failures", async () => {
    const client = new IntentClient({
      ...base,
      invoke: async () => {
        throw new Error("connection closed");
      }
    });
    const outcome = await client.moveTo(1, 1);
    expect(outcome).toEqual({ status: "failed", message: "connection closed" });
  });
});
