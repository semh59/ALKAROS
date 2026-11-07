import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const htmlPath = resolve(pageDir, "index.html");
const scriptPath = resolve(pageDir, "split-payment.js");

const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const SESSION_ID = "66666666-6666-6666-6666-666666666666";

const unpaid = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };
const partial = { ...unpaid, allocatedTotal: 50, remainingAmount: 50 };

async function open({ cash = true, summary = unpaid, tenderStatus = 200, tenderDelayMs = 0 } = {}) {
  const routeSet = [
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: summary },
    cash
      ? { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: SESSION_ID } }
      : { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url, init) => url.endsWith("/cash-tender") && init?.method === "POST", status: tenderStatus, delayMs: tenderDelayMs, body: { approvedAmount: 100, changeAmount: 100 } },
  ];
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter(routeSet);
  await loadApp(htmlPath, scriptPath);
  return fetchMock;
}

const tenderedField = () => document.getElementById("tendered-draft");
const quick = (value) => document.querySelector(`[data-quick-cash="${value}"]`);
const preview = () => document.getElementById("change-preview").textContent;
const tenderCalls = (fetchMock) => fetchMock.mock.calls.filter(([url, init]) => url.endsWith("/cash-tender") && init?.method === "POST");
const submit = () => document.getElementById("submit-tender");

describe("split-payment.js quick cash buttons", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("offers six banknote buttons and an exact-amount button as real labelled buttons", async () => {
    await open();
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());

    const buttons = [...document.querySelectorAll("[data-quick-cash]")];
    expect(buttons.map((b) => b.dataset.quickCash)).toEqual(["5", "10", "20", "50", "100", "200", "exact"]);
    expect(buttons.every((b) => b.tagName === "BUTTON" && b.type === "button" && b.classList.contains("sp-btn"))).toBe(true);
    expect(quick("200").textContent).toBe("200 ₺");
    expect(document.querySelector(".sp-quick-cash").getAttribute("aria-label")).toBe("Hızlı nakit tutarı");
    expect(quick("exact").textContent).toBe("Tam tutar");
  });

  it("sets the cash taken to the banknote value instead of adding to it, and shows the change", async () => {
    await open();
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());

    quick("100").click();
    expect(Number(tenderedField().value)).toBe(100);
    quick("50").click();
    expect(Number(tenderedField().value)).toBe(50);
    quick("200").click();
    expect(Number(tenderedField().value)).toBe(200);
    expect(preview()).toBe("Para üstü: ₺100,00");
  });

  it("sends the chosen banknote as the tendered amount", async () => {
    const fetchMock = await open();
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());

    quick("200").click();
    submit().click();

    await vi.waitFor(() => expect(fetchMock.mock.calls.some(([url, init]) => url.endsWith("/cash-tender") && init?.method === "POST")).toBe(true));
    const call = fetchMock.mock.calls.find(([url, init]) => url.endsWith("/cash-tender") && init?.method === "POST");
    expect(JSON.parse(call[1].body)).toMatchObject({ AmountDue: 100, TenderedAmount: 200 });
  });

  it("sends the exact amount as the tendered amount", async () => {
    const fetchMock = await open({ summary: partial });
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());

    quick("exact").click();
    submit().click();

    await vi.waitFor(() => expect(tenderCalls(fetchMock).length).toBe(1));
    expect(JSON.parse(tenderCalls(fetchMock)[0][1].body)).toMatchObject({ AmountDue: 50, TenderedAmount: 50 });
  });

  it("formats the exact amount with two decimals and ignores it when no amount is set", async () => {
    await open({ summary: { ...unpaid, payableAmount: 33.5, remainingAmount: 33.5 } });
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());

    quick("exact").click();
    expect(tenderedField().value).toBe("33.50");

    const amount = document.getElementById("amount-draft");
    amount.value = "";
    amount.dispatchEvent(new Event("input", { bubbles: true }));
    quick("10").click();
    quick("exact").click();
    expect(tenderedField().value).toBe("10.00");
  });

  it("uses a fresh idempotency key for a different banknote after an unsettled attempt", async () => {
    const fetchMock = await open({ tenderStatus: 503 });
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());

    quick("100").click();
    submit().click();
    await vi.waitFor(() => expect(tenderCalls(fetchMock).length).toBe(1));
    await vi.waitFor(() => expect(submit().disabled).toBe(false));
    quick("200").click();
    submit().click();
    await vi.waitFor(() => expect(tenderCalls(fetchMock).length).toBe(2));

    const keys = tenderCalls(fetchMock).map(([, init]) => JSON.parse(init.body).IdempotencyKey);
    expect(keys[0]).toBeTruthy();
    expect(keys[1]).not.toBe(keys[0]);
  });

  it("ignores the buttons while a payment is being sent", async () => {
    const fetchMock = await open({ tenderDelayMs: 200 });
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());

    quick("100").click();
    submit().click();
    await vi.waitFor(() => expect(tenderCalls(fetchMock).length).toBe(1));
    quick("200").click();

    expect(Number(tenderedField().value)).toBe(100);
  });

  it("keeps the add button off when the banknote does not cover the amount", async () => {
    await open();
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());

    quick("20").click();
    expect(submit().disabled).toBe(true);
    expect(preview()).toContain("tutardan az");

    quick("100").click();
    expect(submit().disabled).toBe(false);
  });

  it("writes the amount being paid for the exact button, following a hand-edited amount", async () => {
    await open({ summary: partial });
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());

    quick("exact").click();
    expect(Number(tenderedField().value)).toBe(50);
    expect(preview()).toBe("Para üstü: ₺0,00");

    const amount = document.getElementById("amount-draft");
    amount.value = "30";
    amount.dispatchEvent(new Event("input", { bubbles: true }));
    quick("exact").click();
    expect(Number(tenderedField().value)).toBe(30);
  });

  it("does not offer the buttons when cash is not available", async () => {
    await open({ cash: false });
    await vi.waitFor(() => expect(document.getElementById("submit-tender")).not.toBeNull());

    expect(document.querySelector("[data-quick-cash]")).toBeNull();
  });
});
