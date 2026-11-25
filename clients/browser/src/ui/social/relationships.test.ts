import { afterEach, describe, expect, it, vi } from "vitest";
import { RelationshipsPanel } from "./relationships";

const status = (overrides: Record<string, unknown> = {}) => ({
  playerId: "p1",
  partnerPlayerId: null,
  partnerGenosNetworkId: null,
  partnerName: null,
  sharesHousehold: false,
  canCohabit: false,
  invitations: [],
  ...overrides
});

const response = (body: unknown) => new Response(JSON.stringify(body), { status: 200 });

describe("relationships panel", () => {
  afterEach(() => {
    document.body.replaceChildren();
    vi.restoreAllMocks();
  });

  it("renders the partner and accepts an incoming invitation", async () => {
    const root = document.createElement("div");
    document.body.appendChild(root);
    const fetchImpl = vi.fn(() => Promise.resolve(response(status({
      partnerPlayerId: "p2",
      partnerGenosNetworkId: 22,
      partnerName: "Bob",
      canCohabit: true,
      invitations: [
        {
          id: "invite-1",
          fromPlayerId: "p3",
          toPlayerId: "p1",
          kind: "Partnership",
          status: "Pending",
          expiresTick: 100,
          createdTick: 10
        }
      ]
    }))));
    const sendAction = vi.fn(async () => ({ status: "sent" as const }));
    const panel = new RelationshipsPanel({
      root,
      apiBaseUrl: "http://localhost",
      token: () => "token",
      fetchImpl,
      sendAction
    });

    await vi.waitFor(() => expect(root.textContent).toContain("Partner: Bob"));
    const accept = Array.from(root.querySelectorAll("button"))
      .find((button) => button.textContent === "Accept")!;
    accept.click();

    await vi.waitFor(() => expect(sendAction).toHaveBeenCalledWith(
      "RespondToInvitation",
      { invitationId: "invite-1", accept: true }
    ));
    expect(fetchImpl).toHaveBeenCalledWith(
      "http://localhost/api/social/relationships",
      { headers: { Authorization: "Bearer token" } }
    );
    panel.dispose();
  });

  it("sends partner, cohabitation and end actions", async () => {
    const root = document.createElement("div");
    document.body.appendChild(root);
    const fetchImpl = vi.fn(() => Promise.resolve(response(status({
      partnerPlayerId: "p2",
      partnerGenosNetworkId: 22,
      partnerName: "Bob",
      canCohabit: true
    }))));
    const sendAction = vi.fn(async () => ({ status: "sent" as const }));
    const panel = new RelationshipsPanel({
      root,
      apiBaseUrl: "http://localhost",
      token: () => "token",
      fetchImpl,
      sendAction
    });
    await vi.waitFor(() => expect(root.textContent).toContain("Bob"));

    const input = root.querySelector<HTMLInputElement>(".social-relationships-invite input")!;
    input.value = "player-3";
    root.querySelector<HTMLFormElement>(".social-relationships-invite")!
      .dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(sendAction).toHaveBeenCalledWith(
      "InvitePartner",
      { targetPlayerId: "player-3" }
    ));

    const cohabit = Array.from(root.querySelectorAll("button"))
      .find((button) => button.textContent === "Cohabit")!;
    cohabit.click();
    await vi.waitFor(() => expect(sendAction).toHaveBeenCalledWith(
      "InviteCohabit",
      { targetPlayerId: "p2" }
    ));

    const end = Array.from(root.querySelectorAll("button"))
      .find((button) => button.textContent === "End partnership")!;
    end.click();
    await vi.waitFor(() => expect(sendAction).toHaveBeenCalledWith(
      "EndPartnership",
      { partnerPlayerId: "p2", splitHousehold: true }
    ));
    panel.dispose();
  });
});
