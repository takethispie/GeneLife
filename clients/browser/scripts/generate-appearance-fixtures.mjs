#!/usr/bin/env node
/**
 * Generates the appearance and stress world fixtures for the browser client.
 *
 * Run from `clients/browser`:
 *   node scripts/generate-appearance-fixtures.mjs
 *
 * Why these exist: every fixture in `src/test/fixtures` predates V5 feature 02
 * and carries no `appearance` field, so `composeAppearance` always fell back to
 * its defaults and the appearance render path had no coverage at all. The
 * `appearance` set walks every value of every appearance dimension at least
 * once; the `stress` set is 2,000 genoses WITH appearance for frame budgeting.
 *
 * Byte ranges mirror `validateAppearance` in `src/contracts/validate.ts`:
 *   skinTone 0..5  faceShape 0..4  hairColor 0..4  hairStyle 0..6  eyeColor 0..4
 *   bodyFrame 0..2  heightScale 0..100  ageBand 0..4  expression 0..3
 *   appearanceVersion 0..255
 */

import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..", "src", "test", "fixtures");
mkdirSync(root, { recursive: true });

function write(name, value) {
  writeFileSync(join(root, name), `${JSON.stringify(value, null, 2)}\n`);
}

/** Map bounds, mirrored from map-snapshot.json (32x32 centred on the origin). */
const MIN_X = -16;
const MAX_X = 15;
const MIN_Y = -16;
const MAX_Y = 15;

const DIMENSIONS = {
  skinTone: 6,
  faceShape: 5,
  hairColor: 5,
  hairStyle: 7,
  eyeColor: 5,
  bodyFrame: 3,
  ageBand: 5,
  expression: 4
};

/** Deterministic 32-bit hash so regenerating the fixtures is reproducible. */
function hash32(value) {
  let h = 2166136261;
  for (let i = 0; i < value.length; i++) {
    h ^= value.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return h >>> 0;
}

function appearance(overrides = {}) {
  return {
    skinTone: 2,
    faceShape: 0,
    hairColor: 1,
    hairStyle: 0,
    eyeColor: 0,
    bodyFrame: 1,
    heightScale: 80,
    ageBand: 2,
    expression: 0,
    appearanceVersion: 1,
    ...overrides
  };
}

function genos(networkId, name, overrides = {}) {
  return {
    networkId,
    name,
    age: 25,
    isPlayerControlled: false,
    hunger: 80,
    energy: 70,
    hygiene: 65,
    bladder: 60,
    currentAction: "Idle",
    timeRemaining: 0,
    x: 0,
    y: 0,
    tileX: 0,
    tileY: 0,
    hasMoveTarget: false,
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
    isDead: false,
    ...overrides
  };
}

/**
 * Lays `rows` out in a grid centred on the origin and stamps x/y/tileX/tileY.
 * Returns the index of the cell closest to the origin, so the caller can make
 * that genos the player — the client centres the camera on the player on first
 * load, and a grid anchored at a map corner would sit mostly off-screen.
 */
function layoutCentred(rows, columns, spacing) {
  const gridRows = Math.ceil(rows.length / columns);
  let centreIndex = 0;
  let bestDistance = Number.POSITIVE_INFINITY;

  rows.forEach((row, index) => {
    const column = index % columns;
    const gridRow = Math.floor(index / columns);
    const x = Math.round((column - (columns - 1) / 2) * spacing);
    const y = Math.round((gridRow - (gridRows - 1) / 2) * spacing);
    row.x = x;
    row.y = y;
    row.tileX = x;
    row.tileY = y;

    const distance = x * x + y * y;
    if (distance < bestDistance) {
      bestDistance = distance;
      centreIndex = index;
    }
  });

  return centreIndex;
}

/**
 * One genos per value of every dimension, so each part/palette the composer can
 * emit is exercised at least once. The other dimensions rotate off the index so
 * the set also covers combinations rather than a single baseline row.
 */
function appearanceGenoses() {
  const rows = [];
  let id = 1000;
  let cursor = 0;

  for (const [dimension, count] of Object.entries(DIMENSIONS)) {
    for (let value = 0; value < count; value++) {
      const overrides = { [dimension]: value };
      // Rotate the remaining dimensions so combinations vary across the grid.
      const others = Object.keys(DIMENSIONS).filter((key) => key !== dimension);
      others.forEach((key, index) => {
        overrides[key] = (cursor + index * 2) % DIMENSIONS[key];
      });
      overrides.heightScale = 60 + ((cursor * 7) % 41);
      overrides.appearanceVersion = 1;

      rows.push(
        genos(id++, `${dimension}-${value}`, {
          age: 10 + ((cursor * 9) % 70),
          appearance: appearance(overrides)
        })
      );
      cursor++;
    }
  }

  // Explicit boundary values that no dimension walk reaches on its own.
  const extremes = [
    { label: "height-min", appearance: appearance({ heightScale: 50 }) },
    { label: "height-max", appearance: appearance({ heightScale: 100 }) },
    {
      label: "version-max",
      appearance: appearance({ appearanceVersion: 255, hairStyle: 6, ageBand: 4 })
    },
    {
      label: "all-max",
      appearance: appearance({
        skinTone: 5,
        faceShape: 4,
        hairColor: 4,
        hairStyle: 6,
        eyeColor: 4,
        bodyFrame: 2,
        heightScale: 100,
        ageBand: 4,
        expression: 3,
        appearanceVersion: 255
      })
    },
    {
      label: "all-min",
      appearance: appearance({
        skinTone: 0,
        faceShape: 0,
        hairColor: 0,
        hairStyle: 0,
        eyeColor: 0,
        bodyFrame: 0,
        heightScale: 50,
        ageBand: 0,
        expression: 0,
        appearanceVersion: 0
      })
    }
  ];

  extremes.forEach((extreme) => {
    rows.push(genos(id++, extreme.label, { age: 30, appearance: extreme.appearance }));
  });

  // The player-controlled genos drives the local ring/label path, and the camera
  // centres on it — so it sits at the middle of the grid.
  const centre = layoutCentred(rows, 8, 3);
  rows[centre].isPlayerControlled = true;
  rows[centre].name = "Ada";

  return rows;
}

/** 2,000 genoses with real appearance values, spread evenly over the map. */
function stressGenoses(count = 2000) {
  const rows = [];
  const columns = 50;
  const spacingX = (MAX_X - MIN_X) / columns;
  const spacingY = (MAX_Y - MIN_Y) / Math.ceil(count / columns);

  for (let index = 0; index < count; index++) {
    const seed = hash32(`stress-${index}`);
    rows.push(
      genos(2000 + index, `genos-${index}`, {
        age: 5 + (seed % 85),
        appearance: appearance({
          skinTone: seed % 6,
          faceShape: (seed >>> 3) % 5,
          hairColor: (seed >>> 6) % 5,
          hairStyle: (seed >>> 9) % 7,
          eyeColor: (seed >>> 12) % 5,
          bodyFrame: (seed >>> 15) % 3,
          heightScale: 50 + (seed % 51),
          ageBand: (seed >>> 18) % 5,
          expression: (seed >>> 21) % 4,
          appearanceVersion: 1
        })
      })
    );
  }

  // Spread over the map, then put the player at the centre so the camera frames
  // the densest part of the field rather than a corner.
  rows.forEach((row, index) => {
    const column = index % columns;
    const gridRow = Math.floor(index / columns);
    row.x = Math.round(MIN_X + column * spacingX);
    row.y = Math.round(MIN_Y + gridRow * spacingY);
    row.tileX = row.x;
    row.tileY = row.y;
  });

  let centreIndex = 0;
  let bestDistance = Number.POSITIVE_INFINITY;
  rows.forEach((row, index) => {
    const distance = row.x * row.x + row.y * row.y;
    if (distance < bestDistance) {
      bestDistance = distance;
      centreIndex = index;
    }
  });

  rows[centreIndex].isPlayerControlled = true;
  rows[centreIndex].name = "StressPlayer";
  return rows;
}

const mapSnapshot = JSON.parse(readFileSync(join(root, "map-snapshot.json"), "utf8"));

write("world-snapshot-appearance.json", {
  tick: 100,
  version: mapSnapshot.version,
  genoses: appearanceGenoses(),
  companies: []
});

write("world-snapshot-stress.json", {
  tick: 100,
  version: mapSnapshot.version,
  genoses: stressGenoses(2000),
  companies: []
});

console.log("Wrote world-snapshot-appearance.json and world-snapshot-stress.json");
