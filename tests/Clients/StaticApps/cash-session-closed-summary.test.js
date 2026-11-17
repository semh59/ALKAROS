import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/cash-session");
const htmlPath = resolve(pageDir, "index.html");
const scriptPath = resolve(pageDir, "cash-session.js");

const TERMINAL = "11111111-1111-1111-1111-111111111111";
const SESSION = "22222222-2222-2222-2222-222222222222";
const session = { cashSessionId: SESSION, status: "Open", openingBalance: 1000, openedAt: "2026-11-03T08:00:00Z" };
const emptyList = { movements: [], movementsTruncated: false, requests: [], approvalThreshold: 500, selfApprovalEnabled: false };

const reply = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body, headers: { get: () => null } });
const $ = (selector) => document.querySelector(selector);

function install(closeBody) {
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    if (url.endsWith("/auth/session/current")) return reply(200, { userId: "u1", displayName: "Ayşe", terminalId: TERMINAL, capabilities: [] });
    if (url.endsWith("/cash-sessions/active")) return reply(200, session);
    if (url.endsWith("/expected-cash")) return reply(200, { expectedCash: 1000, varianceTolerance: 50, openBillCount: 0, unsettledPaymentCount: 0 });
    if (url.endsWith("/cash-movements") && method === "GET") return reply(200, emptyList);
    if (url.endsWith("/start-count")) return reply(200, { ...session, status: "Counting" });
    if (url.endsWith("/counts")) return reply(200, {});
    if (url.endsWith("/close")) return reply(200, { session: { ...session, closedAt: "2026-11-03T20:00:00Z" }, difference: 0, ...closeBody });
    throw new Error(`unrouted ${method} ${url}`);
  }));
}

async function closeWith(closeBody) {
  install(closeBody);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect($("#start-count")).not.toBeNull());
  $("#start-count").click();
  await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());
  $("#denom-100").value = "10";
  $("#denom-100").dispatchEvent(new Event("input", { bubbles: true }));
  $("#submit-count").click();
  await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());
  $("#submit-close").click();
  await vi.waitFor(() => expect(document.body.textContent).toContain("Kasa Kapatıldı"));
}

const summary = () => document.body.textContent;
const row = (id) => {
  const element = $(id);
  return element && { label: element.firstElementChild.textContent, value: element.querySelector(".value").textContent };
};

describe("cash-session.js closed summary: drawer lines", () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, "", "/");
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it.each([
    ["no drawer answer at all", {}],
    ["a drawer that was never used", { drawer: { saleOpenCount: 0, noSaleCount: 0, kickFailedCount: 0 } }],
  ])("shows no drawer line for %s", async (_name, body) => {
    await closeWith(body);

    expect(summary().toLowerCase()).not.toContain("çekmece");
    expect($("#closed-drawer-no-sale")).toBeNull();
    expect($("#closed-drawer-failed")).toBeNull();
  });

  it("shows the no-sale count, and only that, when pulses all arrived", async () => {
    await closeWith({ drawer: { saleOpenCount: 3, noSaleCount: 2, kickFailedCount: 0 } });

    expect(row("#closed-drawer-no-sale")).toEqual({ label: "Satışsız çekmece açma", value: "2" });
    expect($("#closed-drawer-failed")).toBeNull();
  });

  it("says zero no-sale openings when the drawer was used for sales only", async () => {
    await closeWith({ drawer: { saleOpenCount: 1, noSaleCount: 0, kickFailedCount: 0 } });

    expect(row("#closed-drawer-no-sale")).toEqual({ label: "Satışsız çekmece açma", value: "0" });
  });

  it("shows both lines when every pulse failed, so a drawer that never opened is still reported", async () => {
    await closeWith({ drawer: { saleOpenCount: 0, noSaleCount: 0, kickFailedCount: 2 } });

    expect(row("#closed-drawer-no-sale")).toEqual({ label: "Satışsız çekmece açma", value: "0" });
    expect(row("#closed-drawer-failed")).toEqual({ label: "Açılamayan çekmece denemesi", value: "2" });
  });

  it("adds the failed attempts line when pulses did not arrive", async () => {
    await closeWith({ drawer: { saleOpenCount: 0, noSaleCount: 1, kickFailedCount: 4 } });

    expect(row("#closed-drawer-no-sale")).toEqual({ label: "Satışsız çekmece açma", value: "1" });
    expect(row("#closed-drawer-failed")).toEqual({ label: "Açılamayan çekmece denemesi", value: "4" });
  });
});
