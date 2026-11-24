import { afterEach, describe, expect, it, vi } from "vitest";
import { VenuePanel } from "./venue";

const venue = (overrides: Record<string, unknown> = {}) => ({
  venueNetworkId: 900,
  name: "Central Cafe",
  kind: "Cafe",
  occupancy: 2,
  capacity: 4,
  isOpen: true,
  isClosed: false,
  activities: "Sit, Talk, ShareMeal, Cheer",
  occupants: [
    { playerId: "bob", displayName: "Bob", simNetworkId: 22 }
  ],
  recentActivities: [
    { playerId: "bob", displayName: "Bob", activity: "Talk", tick: 8 }
  ],
  ...overrides
});

const response = (body: unknown, status = 200) =>
  new Response(status === 204 ? null : JSON.stringify(body), { status });

describe("venue panel", () => {
  afterEach(() => {
    document.body.replaceChildren();
    vi.restoreAllMocks();
  });

  it("renders occupancy and sends an allowed activity", async () => {
    const root = document.createElement("div");
    document.body.appendChild(root);
    const fetchImpl = vi.fn(() => Promise.resolve(response(venue())));
    const sendAction = vi.fn(async () => ({ status: "sent" as const }));
    const panel = new VenuePanel({
      root,
      apiBaseUrl: "http://localhost",
      token: () => "token",
      fetchImpl,
      sendAction
    });

    await vi.waitFor(() => expect(root.textContent).toContain("Central Cafe"));
    expect(root.textContent).toContain("2/4");
    expect(root.textContent).toContain("Bob");

    const shareMeal = root.querySelector<HTMLButtonElement>('[data-activity="ShareMeal"]')!;
    expect(shareMeal.disabled).toBe(false);
    shareMeal.click();

    await vi.waitFor(() => expect(sendAction).toHaveBeenCalledWith(
      "PerformVenueActivity",
      { activity: "ShareMeal" }
    ));
    expect(fetchImpl).toHaveBeenCalledWith(
      "http://localhost/api/social/venues/current",
      { headers: { Authorization: "Bearer token" } }
    );
    panel.dispose();
  });

  it("shows no venue and disables activities outside a venue", async () => {
    const root = document.createElement("div");
    document.body.appendChild(root);
    const panel = new VenuePanel({
      root,
      apiBaseUrl: "http://localhost",
      token: () => "token",
      fetchImpl: vi.fn(() => Promise.resolve(response(null, 204))),
      sendAction: vi.fn()
    });

    await vi.waitFor(() => expect(root.textContent).toContain("Not in a venue"));
    const playGame = root.querySelector<HTMLButtonElement>('[data-activity="PlayGame"]')!;
    expect(playGame.disabled).toBe(true);
    panel.dispose();
  });
});
