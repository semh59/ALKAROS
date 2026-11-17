import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/cash-session");
const TERMINAL = "11111111-1111-1111-1111-111111111111";
const SESSION = "22222222-2222-2222-2222-222222222222";
const session = { cashSessionId: SESSION, status: "Open", openingBalance: 1000, openedAt: "2026-11-03T08:00:00Z" };
const emptyList = { movements: [], movementsTruncated: false, requests: [], approvalThreshold: 500, selfApprovalEnabled: false };
const reply = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body, headers: { get: () => null } });
const $ = (selector) => document.querySelector(selector);

function install({ varianceExceeded = false, openBills = 0 } = {}) {
  const calls = [];
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    calls.push({ url, method, body: init?.body ? JSON.parse(init.body) : null });
    if (url.endsWith("/auth/session/current")) return reply(200, { userId: "u1", displayName: "Ayşe", terminalId: TERMINAL, capabilities: [] });
    if (url.endsWith("/cash-sessions/active")) return reply(200, session);
    if (url.endsWith("/expected-cash")) return reply(200, { expectedCash: 1000, varianceTolerance: 50, openBillCount: openBills, unsettledPaymentCount: 0 });
    if (url.endsWith("/cash-movements") && method === "GET") return reply(200, emptyList);
    if (url.endsWith("/start-count")) return reply(200, { ...session, status: "Counting" });
    if (url.endsWith("/counts")) return reply(200, {});
    if (url.endsWith("/close") && varianceExceeded) return reply(409, { error: { code: "CASH_VARIANCE_THRESHOLD_EXCEEDED", message: "x" } });
    if (url.endsWith("/close")) return reply(200, { session: { ...session, closedAt: "2026-11-03T20:00:00Z" }, difference: 0 });
    throw new Error(`unrouted ${method} ${url}`);
  }));
  return calls;
}

function type(selector, value) {
  const element = $(selector);
  element.value = value;
  element.dispatchEvent(new Event("input", { bubbles: true }));
}

async function toClosing(hundreds, options) {
  const calls = install(options);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "cash-session.js"));
  await vi.waitFor(() => expect($("#start-count")).not.toBeNull());
  $("#start-count").click();
  $("#start-count-confirm").click();
  await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());
  type("#denom-100", String(hundreds));
  $("#submit-count").click();
  await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());
  return calls;
}

const sent = (calls, suffix) => calls.filter((c) => c.method === "POST" && c.url.endsWith(suffix));

describe("cash-session.js closing screen: recount and ready reasons", () => {
  beforeEach(() => { vi.resetModules(); window.history.replaceState({}, "", "/"); });
  afterEach(() => { vi.unstubAllGlobals(); document.body.innerHTML = ""; });

  it("takes the cashier back to the count with the denomination entries still filled and closes with the corrected count", async () => {
    const calls = await toClosing(10);
    expect($("#recount").textContent).toBe("Sayımı düzelt");

    $("#recount").click();
    await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());
    expect($("#denom-100").value).toBe("10");
    expect($("#counted-amount")).toBeNull();
    type("#denom-100", "9");
    $("#submit-count").click();
    await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());

    expect(sent(calls, "/counts").map((c) => c.body.CountedAmount)).toEqual([1000, 900]);
    expect(document.body.textContent).toContain("Sayılan₺900,00");
    type("#difference-explanation", "Sayım hatası");
    $("#submit-close").click();
    await vi.waitFor(() => expect(sent(calls, "/close")).toHaveLength(1));
    expect(sent(calls, "/close")[0].body.ActualCash).toBe(900);
  });

  it("keeps a single typed amount and the count note when the cashier goes back to correct the count", async () => {
    const calls = await toClosing(10);
    $("#recount").click();
    await vi.waitFor(() => expect($("#toggle-count-mode")).not.toBeNull());
    $("#toggle-count-mode").click();
    await vi.waitFor(() => expect($("#counted-amount")).not.toBeNull());
    type("#counted-amount", "870.50");
    type("#count-notes", "bozuk para eksik");
    $("#submit-count").click();
    await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());

    $("#recount").click();
    await vi.waitFor(() => expect($("#counted-amount")).not.toBeNull());

    expect($("#counted-amount").value).toBe("870.50");
    expect($("#count-notes").value).toBe("bozuk para eksik");
    expect(sent(calls, "/counts").map((c) => c.body.CountedAmount)).toEqual([1000, 870.5]);
  });

  it("offers ready reasons for a difference and a tap writes the text into the explanation", async () => {
    const calls = await toClosing(9);
    const presets = [...document.querySelectorAll("[data-reason-preset]")];

    expect(presets.length).toBeGreaterThanOrEqual(3);
    presets[1].click();
    expect($("#difference-explanation").value).toBe(presets[1].textContent);
    $("#submit-close").click();
    await vi.waitFor(() => expect(sent(calls, "/close")).toHaveLength(1));
    expect(sent(calls, "/close")[0].body.DifferenceExplanation).toBe(presets[1].textContent);
  });

  it("keeps the ready reasons away from the manager's approval note when the difference is over the limit", async () => {
    const calls = await toClosing(5, { varianceExceeded: true });
    type("#difference-explanation", "fark");
    $("#submit-close").click();
    await vi.waitFor(() => expect($("#override-reason")).not.toBeNull());

    expect(document.querySelectorAll("[data-reason-preset]")).toHaveLength(0);
    expect(sent(calls, "/close")).toHaveLength(1);
  });

  it("asks for the acknowledgement of open business again after a recount", async () => {
    await toClosing(10, { openBills: 2 });
    $("#ack-unsettled").click();
    await vi.waitFor(() => expect($("#ack-unsettled").checked).toBe(true));

    $("#recount").click();
    await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());
    $("#submit-count").click();
    await vi.waitFor(() => expect($("#ack-unsettled")).not.toBeNull());

    expect($("#ack-unsettled").checked).toBe(false);
    expect($("#submit-close").disabled).toBe(true);
  });

  it("shows no ready reasons when the count matches", async () => {
    await toClosing(10);

    expect(document.querySelectorAll("[data-reason-preset]")).toHaveLength(0);
    expect($("#difference-explanation")).toBeNull();
  });
});
