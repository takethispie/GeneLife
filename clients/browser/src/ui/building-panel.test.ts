import { describe, expect, it } from "vitest";
import type { CompanyStatus, GenosStatus, MapBuilding } from "../contracts/hub";
import {
  OCCUPANT_LIST_LIMIT,
  describeBuilding,
  describeOwner,
  isInsideFootprint,
  type WorldPopulation
} from "./building-panel";

const office: MapBuilding = {
  networkId: 1,
  kind: "office",
  name: "Foundry",
  district: "commercial",
  x: 2,
  y: 3,
  width: 4,
  height: 3,
  entryX: 3,
  entryY: 6,
  ownerNetworkId: 9
};

const house: MapBuilding = {
  networkId: 5,
  kind: "house",
  name: "Casa Alba",
  district: "residential",
  x: 10,
  y: 10,
  width: 3,
  height: 2,
  entryX: 10,
  entryY: 12,
  ownerNetworkId: null
};

const school: MapBuilding = {
  ...house,
  networkId: 7,
  kind: "school",
  name: "Schoolhouse",
  ownerNetworkId: null
};

function genos(overrides: Partial<GenosStatus> & { networkId: number }): GenosStatus {
  return {
    name: `Genos ${overrides.networkId}`,
    age: 30,
    isPlayerControlled: false,
    hunger: 100,
    energy: 100,
    hygiene: 100,
    bladder: 100,
    currentAction: "Idle",
    timeRemaining: 0,
    x: 0,
    y: 0,
    tileX: 0,
    tileY: 0,
    hasMoveTarget: false,
    moveTargetX: 0,
    moveTargetY: 0,
    walletBalance: 0,
    savingsBalance: 0,
    unmetDemand: 0,
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

function company(networkId: number, name: string): CompanyStatus {
  return {
    networkId,
    name,
    employeeCount: 3,
    officeCount: 1,
    balance: 0,
    reservedForPayroll: 0,
    arrears: 0,
    revenueLastCycle: 0,
    wagesLastCycle: 0,
    solvency: "solvent"
  };
}

function population(genoses: GenosStatus[], companies: CompanyStatus[] = []): WorldPopulation {
  return { genoses, companies };
}

/** A genos inside `house` (10..12 on x, 10..11 on y). */
function resident(networkId: number, overrides: Partial<GenosStatus> = {}): GenosStatus {
  return genos({
    networkId,
    housingBuildingNetworkId: house.networkId,
    housingUnitNetworkId: 30,
    x: 10,
    y: 10,
    ...overrides
  });
}

/** A genos assigned to `office` (2..5 on x, 3..5 on y). */
function worker(networkId: number, overrides: Partial<GenosStatus> = {}): GenosStatus {
  return genos({
    networkId,
    workplaceBuildingNetworkId: office.networkId,
    x: 2,
    y: 3,
    ...overrides
  });
}

describe("describeBuilding", () => {
  it("formats building details", () => {
    const details = describeBuilding(office);
    expect(details.name).toBe("Foundry");
    expect(details.meta).toContain("4×3");
    expect(details.meta).toContain("entry (3, 6)");
    expect(details.hint).toMatch(/server/);
    expect(details.hint).toMatch(/entrance/);
    // The panel must not tell the player to do the walking manually.
    expect(details.hint).not.toMatch(/walk to the entrance first/i);
  });

  it("handles missing owner and name", () => {
    const details = describeBuilding({ ...office, name: "", ownerNetworkId: null });
    expect(details.name).toBe("Building");
    expect(details.owner).toBe("No owner");
  });

  it("never renders HTML from server strings", () => {
    const details = describeBuilding(
      { ...office, name: "<img src=x onerror=alert(1)>", ownerNetworkId: 42 },
      population([genos({ networkId: 42, name: "<b>Alba</b>" })])
    );
    // The HUD writes these with textContent; the model must not strip or mangle.
    expect(details.name).toBe("<img src=x onerror=alert(1)>");
    expect(details.owner).toBe("<b>Alba</b>");
  });
});

describe("describeOwner", () => {
  it("resolves a genos owner to the genos name", () => {
    expect(
      describeOwner(
        { ...office, ownerNetworkId: 42 },
        population([genos({ networkId: 42, name: "Alba" })])
      )
    ).toBe("Alba");
  });

  it("resolves a company owner to the company name", () => {
    expect(
      describeOwner({ ...office, ownerNetworkId: 9 }, population([], [company(9, "GeneCorp")]))
    ).toBe("GeneCorp");
  });

  it("keeps an explicit no-owner label", () => {
    expect(describeOwner({ ...office, ownerNetworkId: null }, population([]))).toBe("No owner");
    expect(describeOwner({ ...office, ownerNetworkId: undefined }, population([]))).toBe(
      "No owner"
    );
  });

  it("falls back to a neutral label for an id that resolves to neither", () => {
    expect(describeOwner({ ...office, ownerNetworkId: 404 }, population([]))).toBe(
      "Owner #404 (unknown)"
    );
  });
});

describe("isInsideFootprint", () => {
  it("treats the footprint as half-open [x, x + width)", () => {
    // Inside.
    expect(isInsideFootprint(house, genos({ networkId: 1, x: 10, y: 10 }))).toBe(true);
    expect(isInsideFootprint(house, genos({ networkId: 1, x: 12.4, y: 11.9 }))).toBe(true);
    // Outside.
    expect(isInsideFootprint(house, genos({ networkId: 1, x: 9.99, y: 10 }))).toBe(false);
    expect(isInsideFootprint(house, genos({ networkId: 1, x: 10, y: 9.5 }))).toBe(false);
    // The far edge belongs to the next tile, so the boundary never flickers.
    expect(isInsideFootprint(house, genos({ networkId: 1, x: 13, y: 10 }))).toBe(false);
    expect(isInsideFootprint(house, genos({ networkId: 1, x: 10, y: 12 }))).toBe(false);
  });
});

describe("describeBuilding occupants", () => {
  it("lists a housing building's residents grouped by unit", () => {
    const details = describeBuilding(
      house,
      population([
        resident(2, { name: "Bruno", housingUnitNetworkId: 31, housingUnitNumber: "B", x: 20 }),
        resident(1, { name: "Alba", housingUnitNetworkId: 30, housingUnitNumber: "A", x: 10 }),
        resident(3, { name: "Carla", housingUnitNetworkId: 30, housingUnitNumber: "A", x: 11 })
      ])
    );

    expect(details.occupants).not.toBeNull();
    expect(details.occupants?.kind).toBe("residents");
    expect(details.occupants?.title).toBe("Residents");
    expect(details.occupants?.empty).toBeNull();

    const groups = details.occupants?.groups ?? [];
    expect(groups.map((group) => group.label)).toEqual(["A", "B"]);
    // Rows are ordered by network id so they do not jump between frames.
    expect(groups[0]?.rows.map((row) => row.name)).toEqual(["Alba", "Carla"]);
    expect(groups[0]?.rows.map((row) => row.present)).toEqual([true, true]);
    // Bruno lives in unit B but is standing outside the building.
    expect(groups[1]?.rows.map((row) => row.present)).toEqual([false]);
  });

  it("falls back to `Unit #<id>` when the server sends no unit number", () => {
    const details = describeBuilding(
      house,
      population([resident(1, { housingUnitNetworkId: 30, housingUnitNumber: null })])
    );
    expect(details.occupants?.groups[0]?.label).toBe("Unit #30");
  });

  it("says nobody lives here rather than listing nothing", () => {
    const details = describeBuilding(house, population([]));
    expect(details.occupants?.groups).toHaveLength(0);
    expect(details.occupants?.empty).toBe("Nobody lives here");
  });

  it("lists a company building's workers with their role or action", () => {
    const details = describeBuilding(
      office,
      population([
        worker(1, { name: "Alba", jobPosition: "Welder", x: 2, y: 3 }),
        worker(2, { name: "Bruno", jobPosition: null, currentAction: "Filing", x: 50, y: 50 })
      ])
    );

    expect(details.occupants?.kind).toBe("workers");
    expect(details.occupants?.empty).toBeNull();
    const rows = details.occupants?.groups[0]?.rows ?? [];
    expect(rows.map((row) => row.name)).toEqual(["Alba", "Bruno"]);
    expect(rows.map((row) => row.detail)).toEqual(["Welder", "Filing"]);
    expect(rows.map((row) => row.present)).toEqual([true, false]);
  });

  it("says nobody works here rather than an empty list", () => {
    const details = describeBuilding(office, population([]));
    expect(details.occupants?.groups).toHaveLength(0);
    expect(details.occupants?.empty).toBe("Nobody works here");
  });

  it("shows no occupants section for a school", () => {
    expect(describeBuilding(school, population([resident(1)])).occupants).toBeNull();
  });

  it("shows no occupants section for an unknown kind", () => {
    expect(
      describeBuilding({ ...house, kind: "lot" }, population([resident(1)])).occupants
    ).toBeNull();
  });

  it("drops dead genoses from the lists", () => {
    const details = describeBuilding(
      house,
      population([resident(1, { name: "Alba" }), resident(2, { name: "Ghost", isDead: true })])
    );
    expect(details.occupants?.groups[0]?.rows.map((row) => row.name)).toEqual(["Alba"]);
  });

  it(`caps each list at ${OCCUPANT_LIST_LIMIT} with a +N more count`, () => {
    const residents = Array.from({ length: 40 }, (_value, index) =>
      resident(index + 1, { name: `R${index + 1}` })
    );
    const details = describeBuilding(house, population(residents));
    const group = details.occupants?.groups[0];
    expect(group?.rows).toHaveLength(OCCUPANT_LIST_LIMIT);
    expect(group?.hiddenCount).toBe(40 - OCCUPANT_LIST_LIMIT);
  });

  it("caps the workers list the same way", () => {
    const workers = Array.from({ length: 7 }, (_value, index) => worker(index + 1));
    const details = describeBuilding(office, population(workers));
    const group = details.occupants?.groups[0];
    expect(group?.rows).toHaveLength(OCCUPANT_LIST_LIMIT);
    expect(group?.hiddenCount).toBe(2);
  });

  it("resolves the owner and the occupants from one-shot iterators", () => {
    // The HUD feeds Map#values() iterators; the owner lookup must not consume
    // the stream before the occupancy scan runs.
    const genoses = new Map(
      [resident(1, { name: "Ada" }), resident(2, { name: "Cleo" })].map((genos) => [
        genos.networkId,
        genos
      ])
    );
    const details = describeBuilding(house, {
      genoses: genoses.values(),
      companies: new Map<number, CompanyStatus>().values()
    });
    expect(details.owner).toBe("No owner");
    expect(details.occupants?.groups[0]?.rows.map((row) => row.name)).toEqual(["Ada", "Cleo"]);
  });

  it("ignores genoses assigned to other buildings", () => {
    const details = describeBuilding(
      house,
      population([
        resident(1, { housingBuildingNetworkId: 99 }),
        genos({ networkId: 2, housingBuildingNetworkId: undefined })
      ])
    );
    expect(details.occupants?.empty).toBe("Nobody lives here");
  });
});
