import { expect, test as base, type Page } from "@playwright/test";
import {
  mockFamilyApi,
  type ApiCall,
  type FamilyApiHandle
} from "./family-api";
import {
  emptyFamilyChildrenPage,
  emptyFamilyGuardianships,
  emptyFamilySimsPage
} from "./family-fixtures";

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

async function gotoFamily(page: Page): Promise<void> {
  await page.goto("/family");
  await expect(page.getByRole("heading", { name: "Family", level: 1 })).toBeVisible({
    timeout: 30_000
  });
  await expect(page).toHaveTitle("Family");
}

async function openFamily(page: Page): Promise<void> {
  await gotoFamily(page);
  await expect(page.locator(".alert.alert-danger")).toHaveCount(0);
  const autoRefresh = page.getByRole("checkbox", { name: "Auto-refresh" });
  if (await autoRefresh.isChecked()) await autoRefresh.uncheck();
  await expect(page.locator(".alert.alert-danger")).toHaveCount(0);
}

function tab(page: Page, name: string) {
  return page.getByRole("button", { name, exact: true });
}

function getCall(api: FamilyApiHandle, pathname: string): ApiCall {
  const call = api.calls.find(
    (candidate) => candidate.method === "GET" && candidate.pathname === pathname
  );
  if (call === undefined) throw new Error(`No GET ${pathname} request was recorded`);
  return call;
}

function commandCall(api: FamilyApiHandle, pathname: string): ApiCall {
  const call = api.calls.find(
    (candidate) => candidate.method === "POST" && candidate.pathname === pathname
  );
  if (call === undefined) throw new Error(`No POST ${pathname} request was recorded`);
  return call;
}

test("renders the overview, every tab, and sim detail", async ({ page }) => {
  const api = await mockFamilyApi(page);
  await openFamily(page);

  await expect(page.getByText("118 of 120 sims have traits")).toBeVisible();
  const population = page.locator(".card", { hasText: "Population" }).first();
  await expect(population).toContainText("120");
  await expect(population).toContainText("90 adults");
  await expect(page.getByRole("heading", { name: "Trait distribution" })).toBeVisible();
  await expect(page.getByText("Brown 70")).toBeVisible();
  await expect(page.getByRole("cell", { name: "Ada Lovelace", exact: true })).toBeVisible();
  await expect(page.getByText("Overcrowded", { exact: true })).toBeVisible();
  await expect(page.getByText("Relationship symmetry", { exact: true })).toBeVisible();
  await expect(page.getByText("Trust conservation", { exact: true })).toBeVisible();
  getCall(api, "/api/family");

  await tab(page, "Sims").click();
  await expect(tab(page, "Sims")).toHaveClass(/active/);
  await expect(page.getByRole("cell", { name: /Ada Lovelace #101/ })).toBeVisible();
  await expect(page.getByRole("cell", { name: "Partnered", exact: true })).toBeVisible();
  getCall(api, "/api/family/sims");

  await page.getByRole("button", { name: "Open", exact: true }).first().click();
  await expect(page.getByRole("heading", { name: /Ada Lovelace/ })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Relationships", level: 3 })).toBeVisible();
  await expect(page.getByRole("cell", { name: "Partner", exact: true })).toBeVisible();
  await expect(page.getByText("Byron (#301, 6)")).toBeVisible();
  await expect(page.getByText("No pregnancies.")).toBeVisible();
  getCall(api, "/api/family/sims/101");

  await tab(page, "Relationships").click();
  await expect(page.getByRole("cell", { name: "Seed", exact: true })).toBeVisible();
  await expect(page.getByRole("cell", { name: /Ada Lovelace #101/ })).toBeVisible();
  getCall(api, "/api/family/relationships");

  await tab(page, "Attraction").click();
  const attractionCard = page.locator(".card", { hasText: "Explain" }).first();
  const attractionInputs = attractionCard.getByRole("spinbutton");
  await attractionInputs.nth(0).fill("101");
  await attractionInputs.nth(1).fill("102");
  await page.getByRole("button", { name: "Explain", exact: true }).click();
  await expect(page.getByText("Eligible", { exact: true })).toBeVisible();
  await expect(page.getByText("score 42", { exact: true })).toBeVisible();
  await expect(page.getByRole("cell", { name: "Trait match" })).toBeVisible();
  expect(getCall(api, "/api/family/attraction").search).toContain("simId=101");

  await tab(page, "Pregnancies").click();
  await expect(page.getByRole("cell", { name: "#700", exact: true })).toBeVisible();
  await expect(
    page.getByRole("cell", { name: /Charles Babbage & Ada Lovelace/ })
  ).toBeVisible();
  await expect(page.getByRole("cell", { name: "Active", exact: true })).toBeVisible();
  getCall(api, "/api/family/pregnancies");

  await tab(page, "Children").click();
  await expect(page.getByRole("cell", { name: /Byron #301/ })).toBeVisible();
  await expect(page.getByText("Lovelace Ward (#302)")).toBeVisible();
  getCall(api, "/api/family/children");
  getCall(api, "/api/family/guardianships");

  await tab(page, "Health").click();
  await expect(page.getByRole("cell", { name: "Relationship symmetry" })).toBeVisible();
  await expect(page.getByRole("cell", { name: "Pass", exact: true })).toBeVisible();
  await expect(page.getByRole("link", { name: "Trust #501" })).toHaveAttribute(
    "href",
    "/family"
  );

  expect(api.unhandled).toEqual([]);
});

test("confirmation gates destructive commands and renders command results", async ({ page }) => {
  const api = await mockFamilyApi(page);
  await openFamily(page);
  await tab(page, "Sims").click();

  const partnership = page.locator(".card", { hasText: "Partnership operations" }).first();
  const partnershipInputs = partnership.getByRole("spinbutton");
  await partnershipInputs.nth(0).fill("101");
  await partnershipInputs.nth(1).fill("102");
  await partnership.getByRole("textbox").fill("e2e-end");

  const endButton = partnership.getByRole("button", { name: "End partnership" });
  const partnershipConfirm = partnership.getByRole("checkbox", {
    name: "Confirm destructive change"
  });
  await expect(endButton).toBeDisabled();
  await partnershipConfirm.check();
  await expect(endButton).toBeEnabled();
  await endButton.click();
  await expect(page.locator(".alert.alert-info")).toHaveText("Partnership end queued.");
  await expect(partnershipConfirm).not.toBeChecked();
  await expect(endButton).toBeDisabled();
  expect(commandCall(api, "/api/family/partnerships/end").body).toMatchObject({
    simAId: 101,
    simBId: 102,
    reason: "e2e-end"
  });

  await partnership.getByRole("button", { name: "Force partnership" }).click();
  await expect(page.locator(".alert.alert-info")).toHaveText("Partnership queued.");
  expect(commandCall(api, "/api/family/partnerships/force").body).toMatchObject({
    simAId: 101,
    simBId: 102,
    reason: "e2e-end",
    overrideEligibility: false
  });

  const reproduction = page.locator(".card", { hasText: "Reproduction operations" }).first();
  const reproductionInputs = reproduction.getByRole("spinbutton");
  await reproductionInputs.nth(0).fill("102");
  await reproductionInputs.nth(1).fill("101");
  await reproduction.getByRole("textbox").fill("e2e-pregnancy");
  await reproduction.getByRole("button", { name: "Force pregnancy" }).click();
  await expect(page.locator(".alert.alert-info")).toHaveText("Pregnancy queued.");
  expect(commandCall(api, "/api/family/pregnancies").body).toMatchObject({
    carryingParentId: 102,
    otherParentId: 101,
    reason: "e2e-pregnancy"
  });

  await tab(page, "Pregnancies").click();
  const pregnancyCard = page.locator(".card", { hasText: "Active only" }).first();
  await pregnancyCard.getByRole("spinbutton").fill("700");
  await pregnancyCard.getByRole("textbox").fill("e2e-cancel");
  const cancelButton = pregnancyCard.getByRole("button", { name: "Cancel", exact: true });
  const pregnancyConfirm = pregnancyCard.getByRole("checkbox", {
    name: "Confirm destructive change"
  });
  await expect(cancelButton).toBeDisabled();
  await pregnancyConfirm.check();
  await expect(cancelButton).toBeEnabled();
  await cancelButton.click();
  await expect(page.locator(".alert.alert-info")).toHaveText("Pregnancy cancellation queued.");
  await expect(pregnancyConfirm).not.toBeChecked();
  expect(commandCall(api, "/api/family/pregnancies/700/cancel").body).toMatchObject({
    reason: "e2e-cancel"
  });

  await tab(page, "Children").click();
  const childrenCard = page.locator(".card", { hasText: "Release trust" }).first();
  const childInputs = childrenCard.getByRole("spinbutton");
  await childInputs.nth(0).fill("302");
  await childInputs.nth(1).fill("101");
  await childInputs.nth(2).fill("900");
  await childrenCard.getByRole("textbox").first().fill("e2e-release");

  await childrenCard.getByRole("button", { name: "Assign guardian" }).click();
  await expect(page.locator(".alert.alert-info")).toHaveText("Guardian assignment queued.");
  expect(commandCall(api, "/api/family/guardianships").body).toMatchObject({
    childId: 302,
    guardianId: 101,
    reason: "e2e-release"
  });

  const releaseButton = childrenCard.getByRole("button", { name: "Release trust" });
  const trustConfirm = childrenCard.getByRole("checkbox", {
    name: "Confirm destructive change"
  });
  await expect(releaseButton).toBeDisabled();
  await trustConfirm.check();
  await expect(releaseButton).toBeEnabled();
  await releaseButton.click();
  await expect(page.locator(".alert.alert-info")).toHaveText("Trust release queued.");
  await expect(trustConfirm).not.toBeChecked();
  expect(commandCall(api, "/api/family/trusts/900/release").body).toMatchObject({
    reason: "e2e-release"
  });

  expect(api.unhandled).toEqual([]);
});

test("resets destructive confirmation when switching tabs", async ({ page }) => {
  const api = await mockFamilyApi(page);
  await openFamily(page);
  await tab(page, "Sims").click();

  const partnership = page.locator(".card", { hasText: "Partnership operations" }).first();
  await partnership.getByRole("checkbox", { name: "Confirm destructive change" }).check();

  await tab(page, "Pregnancies").click();
  const pregnancyCard = page.locator(".card", { hasText: "Active only" }).first();
  await expect(
    pregnancyCard.getByRole("checkbox", { name: "Confirm destructive change" })
  ).not.toBeChecked();
  await expect(
    pregnancyCard.getByRole("button", { name: "Cancel", exact: true })
  ).toBeDisabled();

  await tab(page, "Children").click();
  const childrenCard = page.locator(".card", { hasText: "Release trust" }).first();
  await expect(
    childrenCard.getByRole("checkbox", { name: "Confirm destructive change" })
  ).not.toBeChecked();
  await expect(childrenCard.getByRole("button", { name: "Release trust" })).toBeDisabled();

  expect(api.unhandled).toEqual([]);
});

test("renders command failures in the family error alert", async ({ page }) => {
  const api = await mockFamilyApi(page, {
    failures: [{ method: "POST", pathname: "/api/family/partnerships/force", status: 500 }]
  });
  await openFamily(page);
  await tab(page, "Sims").click();

  const partnership = page.locator(".card", { hasText: "Partnership operations" }).first();
  const partnershipInputs = partnership.getByRole("spinbutton");
  await partnershipInputs.nth(0).fill("101");
  await partnershipInputs.nth(1).fill("102");
  await partnership.getByRole("textbox").fill("e2e-fail");
  await partnership.getByRole("button", { name: "Force partnership" }).click();

  const error = page.locator(".alert.alert-danger");
  await expect(error).toBeVisible();
  await expect(error).toContainText("Could not load family data.");
  await expect(error).toContainText("500");
  await expect(page.locator(".alert.alert-info")).toHaveCount(0);

  expect(api.unhandled).toEqual([]);
});

test("renders empty states for paged tabs", async ({ page }) => {
  const api = await mockFamilyApi(page, {
    sims: emptyFamilySimsPage,
    children: emptyFamilyChildrenPage,
    guardianships: emptyFamilyGuardianships
  });
  await openFamily(page);

  await tab(page, "Sims").click();
  await expect(page.getByText("No sims match the filters.")).toBeVisible();

  await tab(page, "Children").click();
  await expect(page.getByText("No children.", { exact: true })).toBeVisible();
  await expect(page.getByText("No guardianships.", { exact: true })).toBeVisible();

  expect(api.unhandled).toEqual([]);
});
