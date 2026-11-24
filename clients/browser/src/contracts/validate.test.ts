import { describe, expect, it } from "vitest";
import mapSnapshot from "../test/fixtures/map-snapshot.json";
import mapDelta from "../test/fixtures/map-delta.json";
import worldSnapshot from "../test/fixtures/world-snapshot.json";
import worldDelta from "../test/fixtures/world-delta.json";
import welcome from "../test/fixtures/welcome.json";
import intentMove from "../test/fixtures/intent-move.json";
import intentUse from "../test/fixtures/intent-use-building.json";
import intentRejected from "../test/fixtures/intent-rejected.json";
import {
  ContractError,
  parseIntent,
  validateIntentRejected,
  validateMapDelta,
  validateMapSnapshot,
  validatePlayerLifecycleNotice,
  validateWelcome,
  validateWorldSnapshot,
  validateWorldSnapshotDelta
} from "./validate";

describe("contract fixtures", () => {
  it("accepts the recorded map snapshot", () => {
    const parsed = validateMapSnapshot(mapSnapshot);
    expect(parsed.mapId).toBe("world");
    expect(parsed.width * parsed.height).toBe(32 * 32);
    expect(parsed.buildings).toHaveLength(3);
    expect(parsed.tiles.length).toBeGreaterThan(0);
  });

  it("accepts the recorded world snapshot and delta", () => {
    const snapshot = validateWorldSnapshot(worldSnapshot);
    expect(snapshot.tick).toBe(100);
    expect(snapshot.sims).toHaveLength(5);
    expect(snapshot.sims.filter((sim) => sim.isPlayerControlled)).toHaveLength(1);

    const delta = validateWorldSnapshotDelta(worldDelta);
    expect(delta.sinceTick).toBe(100);
    expect(delta.removedSims).toEqual([5]);
    expect(delta.full).toBe(false);
  });

  it("accepts map deltas and welcome messages", () => {
    const delta = validateMapDelta(mapDelta);
    expect(delta.fromVersion).toBe(3);
    expect(delta.toVersion).toBe(4);
    expect(validateWelcome(welcome).simNetworkId).toBe(1);
  });

  it("parses both intent discriminators", () => {
    expect(parseIntent(intentMove)).toMatchObject({
      type: "move",
      targetTileX: 12,
      targetTileY: -4,
      sequence: 42
    });
    expect(parseIntent(intentUse)).toMatchObject({
      type: "useBuilding",
      buildingNetworkId: 7002,
      sequence: 43
    });
  });

  it("validates player lifecycle notices", () => {
    const notice = validatePlayerLifecycleNotice({
      playerId: "p1",
      kind: "died",
      simNetworkId: 7,
      previousSimNetworkId: 0,
      name: "Ada",
      age: 70,
      cause: "Age",
      tick: 100,
      canRespawn: true
    });
    expect(notice).toMatchObject({ kind: "died", name: "Ada", canRespawn: true });
    expect(() =>
      validatePlayerLifecycleNotice({
        playerId: "p1",
        kind: "gone",
        simNetworkId: 7,
        tick: 1
      })
    ).toThrowError(/unknown lifecycle kind/);
  });

  it("validates asynchronous intent rejections", () => {
    expect(validateIntentRejected(intentRejected)).toMatchObject({
      code: "blocked",
      sequence: 42
    });
  });
});

describe("contract validation failures", () => {
  it("rejects non-object payloads with a path", () => {
    expect(() => validateWorldSnapshot([])).toThrowError(ContractError);
    expect(() => validateWorldSnapshot([])).toThrowError(/worldSnapshot: expected an object/);
  });

  it("rejects missing required fields", () => {
    const broken = { ...mapSnapshot, tiles: undefined };
    expect(() => validateMapSnapshot(broken)).toThrowError(/expected an array for "tiles"/);
  });

  it("rejects inconsistent bounds", () => {
    const broken = { ...mapSnapshot, maxX: mapSnapshot.minX - 1 };
    expect(() => validateMapSnapshot(broken)).toThrowError(/bounds must satisfy|bounds width does not match/);
  });

  it("rejects an unknown intent discriminator", () => {
    expect(() => parseIntent({ type: "teleport", playerId: "p" })).toThrowError(
      /unknown intent discriminator/
    );
  });

  it("rejects unsupported welcome protocol versions", () => {
    expect(() => validateWelcome({ ...welcome, protocolVersion: 0 })).toThrowError(
      /unsupported protocol version/
    );
  });
});
