import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const paymentsDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments");
const page = (name) => ({
  html: resolve(paymentsDir, name, "index.html"),
  script: resolve(paymentsDir, name, name + ".js"),
  css: resolve(paymentsDir, name, name + ".css"),
});

const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const SESSION_ID = "66666666-6666-6666-6666-666666666666";
const bill = (remaining) => ({
  billId: BILL_ID, payableAmount: 100, allocatedTotal: 100 - remaining, remainingAmount: remaining, allocations: [], unsettledPayment: null,
});
const app = () => document.getElementById("app");

function splitRoutes(tenderStatuses, remainings = [100]) {
  let current = remainings[0];
  const statuses = [...tenderStatuses];
  return [
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", get body() { return bill(current); } },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: SESSION_ID } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    {
      test: (url, init) => url.endsWith("/cash-tender") && init?.method === "POST",
      get status() { return statuses.length > 1 ? statuses.shift() : statuses[0]; },
      get body() {
        if (remainings.length > 1) current = remainings.shift() && remainings[0];
        return { outcome: "Approved", approvedAmount: 50, changeAmount: 0 };
      },
    },
  ];
}

async function openSplit(routeSet) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter(routeSet);
  const p = page("split-payment");
  await loadApp(p.html, p.script);
  return fetchMock;
}

const tenderKeys = (fetchMock) => fetchMock.mock.calls
  .filter(([url, init]) => url.endsWith("/cash-tender") && init?.method === "POST")
  .map(([, init]) => JSON.parse(init.body).IdempotencyKey);

describe("Cashier payment pages: failing server and slow network", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("keeps the same idempotency key when the server faulted and the cashier retries", async () => {
    const fetchMock = await openSplit(splitRoutes([500, 200], [100]));
    await vi.waitFor(() => expect(document.getElementById("submit-tender")).not.toBeNull());

    document.getElementById("submit-tender").click();
    await vi.waitFor(() => expect(app().textContent).toContain("Sunucu tarafında bir sorun"));
    document.getElementById("submit-tender").click();
    await vi.waitFor(() => expect(tenderKeys(fetchMock)).toHaveLength(2));

    const [first, second] = tenderKeys(fetchMock);
    expect(second).toBe(first);
  });

  it("asks for a new key after the server definitively refused the attempt", async () => {
    const fetchMock = await openSplit(splitRoutes([409, 200], [100]));
    await vi.waitFor(() => expect(document.getElementById("submit-tender")).not.toBeNull());

    document.getElementById("submit-tender").click();
    await vi.waitFor(() => expect(app().textContent).toContain("çakışıyor"));
    document.getElementById("submit-tender").click();
    await vi.waitFor(() => expect(tenderKeys(fetchMock)).toHaveLength(2));

    const [first, second] = tenderKeys(fetchMock);
    expect(second).not.toBe(first);
  });

  it("offers the next share after a tender, the last person pays the remainder", async () => {
    await openSplit(splitRoutes([200], [100, 66.67, 33.3]));
    await vi.waitFor(() => expect(document.getElementById("apply-split")).not.toBeNull());
    document.getElementById("split-count").value = "3";
    document.getElementById("split-count").dispatchEvent(new Event("input", { bubbles: true }));
    document.getElementById("apply-split").click();
    expect(document.getElementById("amount-draft").value).toBe("33.33");

    document.getElementById("submit-tender").click();
    await vi.waitFor(() => expect(document.getElementById("amount-draft").value).toBe("33.34"));

    document.getElementById("submit-tender").click();
    await vi.waitFor(() => expect(document.getElementById("amount-draft").value).toBe("33.30"));
  });

  it("shows a retryable error, not the sign-in screen, when the session check hits a server fault", async () => {
    window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
    installFetchRouter([{ test: (url) => url.endsWith("/api/v1/auth/session/current"), status: 500, body: {} }]);
    const p = page("split-payment");
    await loadApp(p.html, p.script);

    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Okunamadı"));
    expect(app().textContent).not.toContain("Oturumunuz sona ermiş");
    expect(document.querySelector('[data-refund-action="reload"]')).not.toBeNull();
  });

  it.each([["cash-session", "Kasa Bilgisi Okunamadı"], ["customer-accounts", "Cari Hesaplar Okunamadı"]])(
    "%s shows a retryable error on a server fault and the sign-in screen only on 401",
    async (name, title) => {
      const p = page(name);
      installFetchRouter([{ test: (url) => url.endsWith("/api/v1/auth/session/current"), status: 503, body: {} }]);
      await loadApp(p.html, p.script);
      await vi.waitFor(() => expect(app().textContent).toContain(title));
      expect(app().textContent).toContain("Tekrar dene");

      document.body.innerHTML = "";
      vi.resetModules();
      installFetchRouter([{ test: (url) => url.endsWith("/api/v1/auth/session/current"), status: 401, body: {} }]);
      await loadApp(p.html, p.script);
      await vi.waitFor(() => expect(app().textContent).not.toContain(title));
      expect(app().textContent).toMatch(/Giriş/);
    });

  it.each(["split-payment", "cash-session", "customer-accounts"])("%s gives every request a timeout", async (name) => {
    const p = page(name);
    window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
    const fetchMock = installFetchRouter([{ test: (url) => url.endsWith("/api/v1/auth/session/current"), status: 500, body: {} }]);
    await loadApp(p.html, p.script);
    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalled());

    expect(fetchMock.mock.calls[0][1].signal).toBeInstanceOf(AbortSignal);
  });

  it("shows clock times in Istanbul time whatever the device zone is", () => {
    for (const name of ["split-payment", "cash-session"]) {
      const source = readFileSync(page(name).script, "utf8");
      const calls = source.match(/new Date\([^)]*\)\.toLocale(Time)?String\([^)]*\)/g) ?? [];
      expect(calls.length, name).toBeGreaterThan(0);
      const bare = calls.filter((call) => /tr-TR'\)|tr-TR"\)/.test(call));
      expect(bare, name).toEqual([]);
    }
    expect(readFileSync(page("cash-session").script, "utf8")).toContain("timeZone: 'Europe/Istanbul'");
    expect(readFileSync(page("split-payment").script, "utf8")).toContain("timeZone: 'Europe/Istanbul'");
  });

  it("keeps a visible focus ring and 48px touch targets on the payment inputs and chips", () => {
    const split = readFileSync(page("split-payment").css, "utf8");
    const cash = readFileSync(page("cash-session").css, "utf8");
    expect(split).toMatch(/\.sp-amount-field:focus-within\s*\{[^}]*outline:\s*3px/);
    expect(cash).toMatch(/\.cs-amount-field:focus-within\s*\{[^}]*outline:\s*3px/);
    expect(split).not.toMatch(/\.sp-amount-field input:focus\s*\{\s*outline:\s*none/);
    expect(split).toMatch(/\.sp-method-chip\s*\{[^}]*min-height:\s*var\(--touch-target\)/);
  });

  it("offers each person's saved share from the POS split as a one-tap amount", async () => {
    const routes = splitRoutes([200], [100]);
    routes.unshift(
      { test: (url) => url.endsWith("/split-design"), body: { allocations: [
        { ownerId: "o1", amount: 30, mode: "ByItem" }, { ownerId: "o2", amount: 50, mode: "ByItem" }, { ownerId: "o1", amount: 20, mode: "ByItem" },
      ] } },
      { test: (url) => url.endsWith("/split-design/owners"), body: [{ ownerId: "o1", label: "Sandalye 1" }, { ownerId: "o2", label: "Sandalye 2" }] },
    );
    await openSplit(routes);
    await vi.waitFor(() => expect(document.querySelectorAll("[data-share]")).toHaveLength(2));
    expect(app().textContent).toContain("Sandalye 1");

    document.querySelector('[data-share="0"]').click();

    expect(document.getElementById("amount-draft").value).toBe("50.00");
  });

  it("tells the cashier a locked card payment needs a manager", async () => {
    const locked = { ...bill(100), unsettledPayment: { paymentId: "p1", status: "RequiresReconciliation", reason: "x", pendingConfirmation: null } };
    const routes = splitRoutes([200], [100]);
    routes[1] = { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: locked };
    await openSplit(routes);

    await vi.waitFor(() => expect(app().textContent).toContain("Manuel mutabakat gerekiyor"));
    expect(app().textContent).toContain("müdüre yönlendirin");
  });
});
