// @vitest-environment jsdom
//
// Accessibility of the till pages in the states a cashier actually meets: the real page scripts run in jsdom against
// a stubbed API and axe scans what they draw. jsdom cannot compute colours, so contrast is proven separately from the
// design tokens the stylesheet declares.

import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";

// Vitest runs with cwd = the PosTerminal package directory.
const wwwroot = resolve(process.cwd(), "..", "Cashier/wwwroot");
const TERMINAL = "11111111-1111-1111-1111-111111111111";
const BILL = "22222222-2222-2222-2222-222222222222";
const CUSTOMER = "33333333-3333-3333-3333-333333333333";

type Route = { test: (url: string, method: string) => boolean; status?: number; body?: unknown };

function stubApi(routes: Route[]): void {
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    const route = routes.find((candidate) => candidate.test(url, method));
    if (!route) throw new Error(`No fetch route matched ${method} ${url}`);
    const status = route.status ?? 200;
    return { ok: status >= 200 && status < 300, status, json: async () => route.body ?? {}, headers: { get: () => null } };
  }));
}

async function loadPage(page: string, script: string): Promise<void> {
  const html = readFileSync(resolve(wwwroot, page, "index.html"), "utf8");
  document.body.innerHTML = /<body>([\s\S]*)<\/body>/.exec(html)![1].replace(/<script[\s\S]*?<\/script>/g, "");
  // The page scripts are plain browser IIFEs: evaluating them in the jsdom global gives each test a fresh closure.
  (0, eval)(readFileSync(resolve(wwwroot, page, script), "utf8"));
}

async function violations(): Promise<string[]> {
  document.documentElement.lang = "tr";
  const report = await axe.run(document.body, { rules: { "color-contrast": { enabled: false }, region: { enabled: false } } });
  return [...report.violations, ...report.incomplete]
    .filter((item) => item.impact === "critical" || item.impact === "serious")
    .map((item) => `${item.id}: ${item.nodes.map((node) => node.html).join(" | ")}`);
}

const waitFor = (check: () => unknown) => vi.waitFor(() => expect(check()).toBeTruthy());
const unpaid = { billId: BILL, payableAmount: 100, allocatedTotal: 30, remainingAmount: 70, allocations: [{ amount: 30 }], unsettledPayment: null };
const locked = { ...unpaid, unsettledPayment: { paymentId: "p1", status: "RequiresReconciliation", reason: "x", pendingConfirmation: null } };

function paymentRoutes(summary: unknown, cash: Route): Route[] {
  return [
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL, userId: "u1", capabilities: [] } },
    { test: (url, method) => url.endsWith("/tenders/") && method === "GET", body: summary },
    cash,
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url) => url.endsWith("/show-on-display"), status: 204 },
  ];
}

async function openPayment(summary: unknown, cash: Route): Promise<void> {
  window.history.replaceState({}, "", `/?billId=${BILL}`);
  stubApi(paymentRoutes(summary, cash));
  await loadPage("payments/split-payment", "split-payment.js");
  await waitFor(() => document.querySelector(".sp-summary-row"));
}

const drawerOpen: Route = { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "s1" } };
const drawerClosed: Route = { test: (url) => url.includes("/cash-sessions/active"), status: 404 };
const drawerUnreadable: Route = { test: (url) => url.includes("/cash-sessions/active"), status: 500 };

describe("till pages in their dynamic states", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("payment page with the drawer open", async () => {
    await openPayment(unpaid, drawerOpen);
    expect(await violations()).toEqual([]);
  });

  it("payment page with the drawer closed (notice with the way to open it, no method chosen)", async () => {
    await openPayment(unpaid, drawerClosed);
    expect(document.getElementById("cash-state-notice")).not.toBeNull();
    expect(await violations()).toEqual([]);
  });

  it("payment page when the drawer state could not be read", async () => {
    await openPayment(unpaid, drawerUnreadable);
    expect(document.getElementById("cash-state-notice")?.textContent).toContain("Kasa durumu okunamadı");
    expect(await violations()).toEqual([]);
  });

  it("payment page with the card reconciliation lock", async () => {
    await openPayment(locked, drawerOpen);
    expect(document.querySelector(".sp-alert-warning")).not.toBeNull();
    expect(await violations()).toEqual([]);
  });

  it("payment page with the bank transfer confirmation and a discount error showing", async () => {
    await openPayment(unpaid, drawerOpen);
    (document.querySelector('.sp-method-chip[data-method="Eft"]') as HTMLButtonElement).click();
    (document.getElementById("submit-discount") as HTMLButtonElement).click();
    expect(document.getElementById("eft-confirm")).not.toBeNull();
    expect(document.getElementById("fold-discount-tip")).toHaveProperty("open", true);
    expect(document.body.textContent).toContain("Tutar sıfırdan büyük olmalı.");
    expect(await violations()).toEqual([]);
  });

  it("payment page with the refund and cancel section open", async () => {
    await openPayment({ ...unpaid, allocations: [], allocatedTotal: 0 }, drawerOpen);
    (document.getElementById("fold-refund-summary") as HTMLElement).click();
    expect(document.getElementById("fold-refund")).toHaveProperty("open", true);
    expect(await violations()).toEqual([]);
  });

  it("cash session page when the active session cannot be read", async () => {
    window.history.replaceState({}, "", "/");
    stubApi([
      { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL, userId: "u1", capabilities: ["cash.drawer"] } },
      { test: (url) => url.includes("/cash-sessions/active"), status: 500 },
    ]);
    await loadPage("payments/cash-session", "cash-session.js");
    await waitFor(() => document.querySelector("#app button, #app a"));
    expect(document.body.textContent).toContain("Kasa Bilgisi Okunamadı");
    expect(await violations()).toEqual([]);
  });

  it("customer accounts page with a failed receipt showing its error", async () => {
    const customer = { customerId: CUSTOMER, name: "Ali Veli", phoneMasked: null, balance: 250, creditLimit: 1000, availableCredit: 750, paymentTermDays: null, taxIdKind: null };
    window.history.replaceState({}, "", "/");
    stubApi([
      { test: (url) => url.endsWith("/auth/session/current"), body: { terminalId: TERMINAL, userId: "u1", capabilities: ["payments.take", "cash.drawer", "settings.manage"] } },
      { test: (url) => url.endsWith("/cash-sessions/active"), body: { cashSessionId: "s1" } },
      { test: (url, method) => url.endsWith("/customers") && method === "GET", body: [customer] },
      { test: (url) => url.endsWith("/statement"), body: { customer, entries: [], receipts: [] } },
      { test: (url) => url.endsWith("/account-receipts"), status: 500 },
    ]);
    await loadPage("payments/customer-accounts", "customer-accounts.js");
    await waitFor(() => document.querySelector(".sp-customer"));
    (document.querySelector(".sp-customer") as HTMLElement).click();
    await waitFor(() => document.getElementById("ca-receive"));
    const amount = document.getElementById("ca-receipt-amount") as HTMLInputElement;
    amount.value = "50";
    amount.dispatchEvent(new Event("input", { bubbles: true }));
    (document.getElementById("ca-receive") as HTMLButtonElement).click();
    await waitFor(() => document.querySelector('[role="alert"]'));
    expect(document.querySelector('[role="alert"]')?.textContent).toContain("Sunucu tarafında bir sorun oluştu");
    expect(await violations()).toEqual([]);
  });
});

function luminance(hex: string): number {
  const channels = [1, 3, 5].map((index) => parseInt(hex.slice(index, index + 2), 16) / 255)
    .map((value) => (value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4));
  return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
}
const contrast = (a: string, b: string) => {
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (light + 0.05) / (dark + 0.05);
};

const stylesheets = ["payments/split-payment/split-payment.css", "payments/cash-session/cash-session.css"];

describe.each(stylesheets)("colour contrast of the tokens in %s", (sheet) => {
  const css = readFileSync(resolve(wwwroot, sheet), "utf8");
  const token = (name: string) => (name.startsWith("#") ? name : new RegExp(`--${name}:\\s*(#[0-9a-fA-F]{6})`).exec(css)?.[1] ?? "");

  it.each([
    ["color-danger", "color-surface"],
    ["color-success", "color-surface"],
    ["color-warning", "color-surface"],
    ["color-brand", "color-surface"],
    ["color-text-muted", "color-surface"],
    ["color-text-muted", "color-canvas"],
    ["color-ink", "color-surface"],
    ["color-text-muted", "color-disabled"],
    ["#FFFFFF", "color-ink"],
    ["#FFFFFF", "color-danger"],
    ["#FFFFFF", "color-warning"],
    ["#FFFFFF", "color-success"],
  ])("%s on %s is at least 4.5:1", (foreground, background) => {
    expect(token(foreground), `${foreground} is declared`).not.toBe("");
    expect(token(background), `${background} is declared`).not.toBe("");
    expect(contrast(token(foreground), token(background))).toBeGreaterThanOrEqual(4.5);
  });

  it("does not fade a disabled button with opacity, which lowers the text contrast", () => {
    expect(css).not.toMatch(/-btn:disabled\s*\{[^}]*opacity/);
  });
});
