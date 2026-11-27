import { describe, expect, it } from "vitest";
import type { GenosStatus } from "../contracts/hub";
import { describeGenos } from "./genos-panel";

const genos: GenosStatus = {
  networkId: 42,
  name: "Ada",
  age: 25,
  isPlayerControlled: false,
  hunger: 80,
  energy: 70,
  hygiene: 65,
  bladder: 60,
  currentAction: "Idle",
  timeRemaining: 0,
  x: 3.4,
  y: 4.25,
  tileX: 3,
  tileY: 4,
  hasMoveTarget: false,
  moveTargetX: 0,
  moveTargetY: 0,
  walletBalance: 100,
  savingsBalance: 50,
  unmetDemand: 0,
  householdNetworkId: 7,
  housingUnitNetworkId: 9,
  tenure: "owner",
  housingTier: "standard",
  homeless: false,
  stage: "Adult",
  health: 100,
  topSkill: "logic",
  seniority: "none",
  isDead: false
};

describe("describeGenos", () => {
  it("maps the basic stats from the snapshot", () => {
    const details = describeGenos(genos);
    expect(details.name).toBe("Ada");
    expect(details.status).toBe("Alive");
    expect(details.meta).toContain("age 25");
    expect(details.meta).toContain("health 100");
    expect(details.action).toBe("Idle");
    expect(details.needs).toContain("hunger 80");
    expect(details.needs).toContain("energy 70");
    expect(details.needs).toContain("hygiene 65");
    expect(details.needs).toContain("bladder 60");
    expect(details.money).toContain("wallet 100");
    expect(details.money).toContain("savings 50");
    expect(details.housing).toContain("standard");
    expect(details.housing).toContain("owner");
    expect(details.household).toBe("Household #7");
    expect(details.housingUnit).toBe("Housing unit #9");
  });

  it("appends the time left for a running action", () => {
    const details = describeGenos({ ...genos, currentAction: "Eating", timeRemaining: 12 });
    expect(details.action).toBe("Eating · 12s left");
  });

  it("shows a dead genos as deceased without hiding its stats", () => {
    const details = describeGenos({ ...genos, isDead: true, health: 0 });
    expect(details.status).toBe("Deceased");
    expect(details.meta).toContain("health 0");
    // A corpse keeps its numbers rather than blanking the panel.
    expect(details.needs).toContain("hunger 80");
  });

  it("handles missing household, housing tier and name", () => {
    const details = describeGenos({
      ...genos,
      name: "",
      householdNetworkId: null,
      housingUnitNetworkId: null,
      housingTier: "",
      tenure: "",
      homeless: true
    });
    expect(details.name).toBe("Genos");
    expect(details.household).toBe("No household");
    expect(details.housingUnit).toBe("No housing unit");
    expect(details.housing).toBe("homeless");
  });
});
