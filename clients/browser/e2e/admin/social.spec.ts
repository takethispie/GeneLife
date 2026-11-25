import { expect, test as base, type Page } from "@playwright/test";

const test = base.extend<{ pageErrors: string[] }>({
  pageErrors: async ({ page }, use) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    await use(errors);
  }
});

test.afterEach(({ pageErrors }) => {
  expect(pageErrors).toEqual([]);
});

const overview = {
  tick: 42,
  online: [
    {
      playerId: "alice",
      displayName: "Alice",
      genosNetworkId: 101,
      tileX: 10,
      tileY: 10,
      buildingNetworkId: 900,
      state: "InVenue",
      lastSeenTick: 42
    }
  ],
  venues: [
    {
      venueNetworkId: 900,
      name: "Central Cafe",
      kind: "Cafe",
      occupancy: 1,
      capacity: 8,
      isOpen: true,
      isClosed: false
    }
  ],
  reports: [
    {
      id: "11111111-1111-1111-1111-111111111111",
      reporterPlayerId: "bob",
      targetPlayerId: "alice",
      reason: "Harassment",
      messageId: null,
      evidence: "evidenced message",
      createdTick: 40,
      status: "Open"
    }
  ],
  edges: [
    {
      fromPlayerId: "alice",
      toPlayerId: "bob",
      kind: "Friend",
      sinceTick: 1,
      source: "Request"
    }
  ],
  requests: [
    {
      id: "22222222-2222-2222-2222-222222222222",
      fromPlayerId: "carol",
      toPlayerId: "alice",
      kind: "Friend",
      status: "Pending",
      createdTick: 30,
      expiresTick: 130
    }
  ],
  partnershipInvitations: [
    {
      id: "33333333-3333-3333-3333-333333333333",
      fromPlayerId: "alice",
      toPlayerId: "bob",
      kind: "Cohabitation",
      status: "Pending",
      createdTick: 35,
      expiresTick: 135
    }
  ],
  moderationActions: [
    {
      id: "44444444-4444-4444-4444-444444444444",
      reportId: "11111111-1111-1111-1111-111111111111",
      reversesActionId: null,
      moderatorId: "admin-1",
      targetPlayerId: "alice",
      counterpartyPlayerId: "bob",
      kind: "Warn",
      durationTicks: 0,
      createdTick: 41,
      endsTick: null,
      reason: "first warning",
      evidence: "evidenced message",
      messageId: null,
      venueNetworkId: null
    }
  ],
  privacy: [
    {
      playerId: "alice",
      showDisplayName: true,
      allowNearbyForNonFriends: false,
      friendsOnlyMessages: true
    }
  ],
  chatRetention: {
    retainedMessages: 12,
    deletedMessages: 1,
    oldestRetained: "2026-01-01T00:00:00+00:00",
    checkedAt: "2026-01-02T00:00:00+00:00",
    retentionDays: 30
  }
};

async function mockSocialApi(page: Page): Promise<void> {
  await page.route("**/appsettings*.json", async (route) => {
    const origin = new URL(route.request().url()).origin;
    await route.fulfill({ json: { AdminPlayerId: "admin-ui", ApiBaseUrl: origin } });
  });

  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (request.method() === "OPTIONS") {
      await route.fulfill({ status: 204 });
      return;
    }
    if (request.method() === "POST" && url.pathname === "/api/auth/local-admin") {
      await route.fulfill({
        json: { accessToken: "e2e-admin-token", expiresAt: "2099-01-01T00:00:00+00:00" }
      });
      return;
    }
    if (request.method() === "GET" && url.pathname === "/api/social/admin/overview") {
      await route.fulfill({ json: overview });
      return;
    }
    await route.fulfill({
      status: 404,
      contentType: "application/json",
      body: JSON.stringify({ error: "unmocked", pathname: url.pathname })
    });
  });
}

test("renders the social moderation overview", async ({ page }) => {
  await mockSocialApi(page);
  await page.goto("/social");
  await expect(page.getByRole("heading", { name: "Social", level: 1 })).toBeVisible({
    timeout: 30_000
  });
  await expect(page).toHaveTitle("Social");
  await expect(page.locator(".alert.alert-danger")).toHaveCount(0);

  await expect(page.getByText("Central Cafe (#900)")).toBeVisible();
  await expect(page.getByText("evidenced message")).toBeVisible();
  await expect(page.getByText("first warning")).toBeVisible();
  await expect(page.getByText("alice", { exact: true }).first()).toBeVisible();
  await expect(page.getByText("12", { exact: true })).toBeVisible();
  await expect(page.getByText("30 day retention")).toBeVisible();
});
