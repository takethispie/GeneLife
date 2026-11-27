import { describe, expect, it } from "vitest";
import type { MapBuilding } from "../contracts/hub";
import { describeBuilding } from "./building-panel";

const building: MapBuilding = {
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

describe("describeBuilding", () => {
  it("formats building details", () => {
    const details = describeBuilding(building);
    expect(details.name).toBe("Foundry");
    expect(details.meta).toContain("4×3");
    expect(details.meta).toContain("entry (3, 6)");
    expect(details.owner).toBe("Owner #9");
    expect(details.hint).toMatch(/server/);
    expect(details.hint).toMatch(/entrance/);
    // The panel must not tell the player to do the walking manually.
    expect(details.hint).not.toMatch(/walk to the entrance first/i);
  });

  it("handles missing owner and name", () => {
    const details = describeBuilding({ ...building, name: "", ownerNetworkId: null });
    expect(details.name).toBe("Building");
    expect(details.owner).toBe("No owner");
  });
});
