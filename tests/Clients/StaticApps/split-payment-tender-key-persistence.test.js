import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const htmlPath = resolve(pageDir, "index.html");
const scriptPath = resolve(pageDir, "split-payment.js");

const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const SESSION_ID = "66666666-6666-6666-6666-666666666666";

const reply = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body, headers: { get: () => null } });
const $ = (selector) => document.querySelector(selector);

/** A scripted server: `world.remaining` is what the bill still owes; `world.tender` answers the cash-tender POST. */
function install(world) {
  const tenders = [];
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    if (url.endsWith("/api/v1/auth/session/current")) return reply(200, { terminalId: TERMINAL_ID, userId: "u1" });
    if (url.endsWith("/tenders/") && method === "GET") {
      return reply(200, { billId: BILL_ID, payableAmount: 100, allocatedTotal: 100 - world.remaining, remainingAmount: world.remaining, allocations: [], unsettledPayment: null });
    }
    if (url.includes("/cash-sessions/active")) return reply(200, { cashSessionId: SESSION_ID, cashierUserId: "u1" });
    if (url.endsWith("/adjustments")) return reply(200, { adjustments: [], summary: null });
    if (url.endsWith("/refund-requests")) return reply(200, []);
    if (url.endsWith("/show-on-display")) return reply(204, {});
    if ((url.endsWith("/cash-tender") || url.endsWith("/tenders/")) && method === "POST") {
      tenders.push(JSON.parse(init.body));
      if (world.networkDown) throw new TypeError("Failed to fetch");
      return world.tender ?? reply(200, { approvedAmount: 100, changeAmount: 0 });
    }
    throw new Error(`unrouted ${method} ${url}`);
  }));
  return tenders;
}

/** Loads the page like a fresh visit (a reload gives the same bill a new page instance). */
async function visit() {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect($("#submit-tender")).not.toBeNull());
}

async function pay(tenders, count) {
  $("#submit-tender").click();
  await vi.waitFor(() => expect(tenders).toHaveLength(count));
  await vi.waitFor(() => expect($("#submit-tender").disabled).toBe(false));
}

describe("split-payment.js tender idempotency key survives a page reload", () => {
  let world;
  beforeEach(() => {
    vi.resetModules();
    window.sessionStorage.clear();
    world = { remaining: 100, networkDown: true };
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("replays the same key after a lost response and a reload of the same bill state", async () => {
    const tenders = install(world);
    await visit();
    await pay(tenders, 1);

    await visit();
    await pay(tenders, 2);

    expect(tenders[1].IdempotencyKey).toBe(tenders[0].IdempotencyKey);
  });

  it("mints a new key when the reload shows the first request had landed (same amount, smaller remaining)", async () => {
    const tenders = install(world);
    await visit();
    $("#amount-draft").value = "40";
    $("#amount-draft").dispatchEvent(new Event("input", { bubbles: true }));
    await pay(tenders, 1);

    world.remaining = 60;
    await visit();
    $("#amount-draft").value = "40";
    $("#amount-draft").dispatchEvent(new Event("input", { bubbles: true }));
    await pay(tenders, 2);

    expect(tenders[1].Amount ?? tenders[1].AmountDue).toBe(tenders[0].Amount ?? tenders[0].AmountDue);
    expect(tenders[1].IdempotencyKey).not.toBe(tenders[0].IdempotencyKey);
  });

  it("mints a new key for a different amount after the reload", async () => {
    const tenders = install(world);
    await visit();
    await pay(tenders, 1);

    await visit();
    $("#amount-draft").value = "40";
    $("#amount-draft").dispatchEvent(new Event("input", { bubbles: true }));
    await pay(tenders, 2);

    expect(tenders[1].IdempotencyKey).not.toBe(tenders[0].IdempotencyKey);
  });

  it("forgets the key once the server has answered for real, so the next attempt is a new payment", async () => {
    world.networkDown = false;
    world.tender = reply(409, { error: { code: "CASH_SESSION_NOT_OWNED", message: "x" } });
    const tenders = install(world);
    await visit();
    await pay(tenders, 1);

    await visit();
    await pay(tenders, 2);

    expect(tenders[1].IdempotencyKey).not.toBe(tenders[0].IdempotencyKey);
  });

  it("mints a new key when the note changes between two retries without a reload", async () => {
    const tenders = install(world);
    await visit();
    $('.sp-method-chip[data-method="Eft"]').click();
    $("#eft-confirm").click();
    await pay(tenders, 1);

    $("#note-draft").value = "farkli not";
    $("#note-draft").dispatchEvent(new Event("input", { bubbles: true }));
    await pay(tenders, 2);

    expect(tenders[1].IdempotencyKey).not.toBe(tenders[0].IdempotencyKey);
  });

  it("does not replay a stored key older than ten minutes", async () => {
    const tenders = install(world);
    const now = Date.now();
    const clock = vi.spyOn(Date, "now").mockReturnValue(now);
    await visit();
    await pay(tenders, 1);

    clock.mockReturnValue(now + 11 * 60 * 1000);
    await visit();
    await pay(tenders, 2);
    clock.mockRestore();

    expect(tenders[1].IdempotencyKey).not.toBe(tenders[0].IdempotencyKey);
  });

  it("keeps the key across a server error (5xx), because that request may have landed", async () => {
    world.networkDown = false;
    world.tender = reply(503, { error: { code: "UNAVAILABLE", message: "x" } });
    const tenders = install(world);
    await visit();
    await pay(tenders, 1);

    await visit();
    await pay(tenders, 2);

    expect(tenders[1].IdempotencyKey).toBe(tenders[0].IdempotencyKey);
  });

  it("keeps working when session storage is unavailable", async () => {
    const tenders = install(world);
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => { throw new Error("blocked"); });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => { throw new Error("blocked"); });
    await visit();
    await pay(tenders, 1);

    expect(tenders[0].IdempotencyKey).toMatch(/^[0-9a-f-]{36}$/);
    vi.restoreAllMocks();
  });
});
