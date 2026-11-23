#!/usr/bin/env node
/**
 * Generates deterministic JSON fixtures for the browser client contract tests.
 *
 * Run from `clients/browser`:
 *   node scripts/generate-fixtures.mjs
 *
 * The fixtures mirror the server's camelCase SignalR payloads. Map tiles are
 * run-length encoded per row exactly like `SimulationEngine.BuildMapTileRuns`.
 */

import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..", "src", "test", "fixtures");
mkdirSync(root, { recursive: true });

function write(name, value) {
  writeFileSync(join(root, name), `${JSON.stringify(value, null, 2)}\n`);
}

function flagsFor(tile, config) {
  const flags = [];
  const isWater =
    tile.x >= config.water.minX &&
    tile.x <= config.water.maxX &&
    tile.y >= config.water.minY &&
    tile.y <= config.water.maxY;
  if (isWater) return "Water";

  flags.push("Land");
  if (tile.x % config.roadEvery === 0 || tile.y % config.roadEvery === 0) flags.push("Road");
  return flags.join(", ");
}

function buildRuns(minX, maxX, minY, maxY, config) {
  const runs = [];
  for (let y = minY; y <= maxY; y++) {
    let startX = minX;
    let current = flagsFor({ x: startX, y }, config);
    for (let x = minX + 1; x <= maxX; x++) {
      const flags = flagsFor({ x, y }, config);
      if (flags === current) continue;
      runs.push({ x: startX, y, length: x - startX, flags: current });
      startX = x;
      current = flags;
    }
    runs.push({ x: startX, y, length: maxX - startX + 1, flags: current });
  }
  return runs;
}

function mapSnapshot({ mapId, version, seed, width, height, roadEvery, water, buildings }) {
  const minX = -(width / 2);
  const minY = -(height / 2);
  const maxX = width - 1 - width / 2;
  const maxY = height - 1 - height / 2;
  return {
    mapId,
    version,
    seed,
    width,
    height,
    tileSize: 1,
    minX,
    minY,
    maxX,
    maxY,
    tiles: buildRuns(minX, maxX, minY, maxY, { roadEvery, water }),
    buildings
  };
}

const smallBuildings = [
  {
    networkId: 7001,
    kind: "house",
    name: "Willow House",
    district: "residential",
    x: -6,
    y: -6,
    width: 3,
    height: 2,
    entryX: -5,
    entryY: -4,
    ownerNetworkId: 42
  },
  {
    networkId: 7002,
    kind: "office",
    name: "Foundry Office",
    district: "commercial",
    x: 2,
    y: 3,
    width: 4,
    height: 3,
    entryX: 3,
    entryY: 6,
    ownerNetworkId: 9
  },
  {
    networkId: 7003,
    kind: "school",
    name: "North School",
    district: "civic",
    x: 7,
    y: -9,
    width: 4,
    height: 2,
    entryX: 9,
    entryY: -7,
    ownerNetworkId: null
  }
];

const smallMap = mapSnapshot({
  mapId: "world",
  version: 3,
  seed: 7,
  width: 32,
  height: 32,
  roadEvery: 8,
  water: { minX: -15, maxX: -12, minY: -15, maxY: -11 },
  buildings: smallBuildings
});

const largeBuildings = smallBuildings.map((building, index) => ({
  ...building,
  networkId: 7001 + index,
  entryX: building.entryX * 4,
  entryY: building.entryY * 4,
  x: building.x * 4,
  y: building.y * 4
}));

const largeMap = mapSnapshot({
  mapId: "world",
  version: 1,
  seed: 7,
  width: 256,
  height: 256,
  roadEvery: 16,
  water: { minX: -120, maxX: -90, minY: 40, maxY: 60 },
  buildings: largeBuildings
});

function sim(networkId, name, tileX, tileY, options = {}) {
  return {
    networkId,
    name,
    age: options.age ?? 25,
    isPlayerControlled: options.isPlayerControlled ?? false,
    hunger: 80,
    energy: 70,
    hygiene: 65,
    bladder: 60,
    currentAction: options.currentAction ?? "Idle",
    timeRemaining: 0,
    x: options.x ?? tileX,
    y: options.y ?? tileY,
    tileX,
    tileY,
    hasMoveTarget: options.hasMoveTarget ?? false,
    moveTargetX: 0,
    moveTargetY: 0,
    walletBalance: 100,
    savingsBalance: 50,
    unmetDemand: 0,
    householdNetworkId: null,
    housingUnitNetworkId: null,
    tenure: "owner",
    housingTier: "standard",
    homeless: false,
    stage: "Adult",
    health: 100,
    topSkill: "logic",
    seniority: "none",
    isDead: false
  };
}

const company = {
  networkId: 9001,
  name: "Foundry",
  employeeCount: 3,
  officeCount: 1,
  balance: 10000,
  reservedForPayroll: 1000,
  arrears: 0,
  revenueLastCycle: 500,
  wagesLastCycle: 400,
  solvency: "healthy"
};

const worldSnapshot = {
  tick: 100,
  version: 2,
  sims: [
    sim(1, "Ada", 0, 0, { isPlayerControlled: true }),
    sim(2, "Boris", 3, 4, { x: 3.4, y: 4.25 }),
    sim(3, "Cleo", -5, 2),
    sim(4, "Dmitri", 8, -8),
    sim(5, "Eve", -2, 6)
  ],
  companies: [company]
};

const worldDelta = {
  tick: 101,
  sinceTick: 100,
  version: 2,
  full: false,
  sims: [
    sim(1, "Ada", 1, 0, { isPlayerControlled: true, x: 0.5, y: 0 }),
    sim(2, "Boris", 4, 4, { x: 3.9, y: 4.1 })
  ],
  companies: [],
  removedSims: [5],
  removedCompanies: []
};

const sims2000 = [];
for (let i = 0; i < 2000; i++) {
  const tileX = (i % 64) - 32;
  const tileY = Math.floor(i / 64) - 16;
  sims2000.push(sim(10000 + i, `Sim_${i}`, tileX, tileY));
}

const worldSnapshot2000 = {
  tick: 500,
  version: 2,
  sims: sims2000,
  companies: [company]
};

write("map-snapshot.json", smallMap);
write("map-snapshot-large.json", largeMap);
write("world-snapshot.json", worldSnapshot);
write("world-delta.json", worldDelta);
write("world-snapshot-2000.json", worldSnapshot2000);
write("map-delta.json", {
  mapId: "world",
  fromVersion: 3,
  toVersion: 4,
  tick: 102,
  added: [
    {
      networkId: 7004,
      kind: "shop",
      name: "Corner Shop",
      district: "commercial",
      x: -3,
      y: 5,
      width: 2,
      height: 2,
      entryX: -3,
      entryY: 7,
      ownerNetworkId: 9
    }
  ],
  updated: [{ ...smallBuildings[1], name: "Foundry Office (rebuilt)" }],
  removed: [7003]
});
write("intent-move.json", {
  type: "move",
  playerId: "player-1",
  sequence: 42,
  simNetworkId: 1,
  targetTileX: 12,
  targetTileY: -4
});
write("intent-use-building.json", {
  type: "useBuilding",
  playerId: "player-1",
  sequence: 43,
  simNetworkId: 1,
  buildingNetworkId: 7002
});
write("snapshot-rejected.json", {
  error: "Rate limit exceeded (5 requests per second)"
});
write("welcome.json", {
  playerId: "player-1",
  simNetworkId: 1,
  mapId: "world",
  mapVersion: 3,
  tick: 100,
  tickIntervalMs: 40,
  snapshotVersion: 2,
  protocolVersion: 1
});
write("intent-rejected.json", {
  playerId: "player-1",
  sequence: 42,
  code: "blocked",
  message: "That tile is blocked"
});

console.log(`Wrote fixtures to ${root}`);
