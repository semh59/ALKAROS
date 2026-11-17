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
// A figure no other number on these screens can produce: the page formats it as "1.234,56".
const EXPECTED = 1234.56;
const EXPECTED_TEXT = /1\.234,56/;
const session = { cashSessionId: SESSION, status: "Open", openingBalance: 1000, openedAt: "2026-11-03T08:00:00Z" };
const emptyList = { movements: [], movementsTruncated: false, requests: [], approvalThreshold: 500, selfApprovalEnabled: false };

const reply = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body, headers: { get: () => null } });
const $ = (selector) => document.querySelector(selector);
const text = () => document.body.textContent;

function install() {
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    if (url.endsWith("/auth/session/current")) return reply(200, { userId: "u1", displayName: "Ayşe", terminalId: TERMINAL, capabilities: [] });
    if (url.endsWith("/cash-sessions/active")) return reply(200, session);
    if (url.endsWith("/expected-cash")) return reply(200, { expectedCash: EXPECTED, varianceTolerance: 50, openBillCount: 0, unsettledPaymentCount: 0 });
    if (url.endsWith("/cash-movements") && method === "GET") return reply(200, emptyList);
    if (url.endsWith("/start-count")) return reply(200, { ...session, status: "Counting" });
    if (url.endsWith("/counts")) return reply(200, {});
    throw new Error(`unrouted ${method} ${url}`);
  }));
}

async function type(selector, value) {
  const element = $(selector);
  element.value = value;
  element.dispatchEvent(new Event("input", { bubbles: true }));
}

async function summaryFor(direction, amount) {
  $(`#cash-${direction}`).click();
  await vi.waitFor(() => expect($("#movement-amount")).not.toBeNull());
  await type("#movement-amount", amount);
  await type("#movement-notes", "market");
  $("#movement-submit").click();
  await vi.waitFor(() => expect($("#movement-confirm")).not.toBeNull());
}

describe("cash-session.js blind count: the expected cash stays hidden until the count is sent", () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, "", "/");
    install();
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("shows no expected amount on the open session or while a cash movement is entered", async () => {
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#cash-out")).not.toBeNull());
    expect(text()).not.toMatch(EXPECTED_TEXT);

    await summaryFor("in", "25");
    expect(text()).not.toMatch(EXPECTED_TEXT);
    expect(text()).not.toContain("Kasadaki beklenen nakit");
  });

  it("still stops a cash-out larger than the drawer without naming the amount", async () => {
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#cash-out")).not.toBeNull());

    await summaryFor("out", "5000");

    expect($("#drawer-warning")).not.toBeNull();
    expect($("#movement-confirm").disabled).toBe(true);
    expect(text()).not.toMatch(EXPECTED_TEXT);
  });

  it("shows no expected amount on the count screen, then shows it once the count is sent, and hides it again on a recount", async () => {
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#start-count")).not.toBeNull());
    $("#start-count").click();
    await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());
    expect(text()).not.toMatch(EXPECTED_TEXT);
    expect(text()).not.toContain("Beklenen");

    await type("#denom-100", "10");
    $("#submit-count").click();
    await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());
    expect(text()).toMatch(EXPECTED_TEXT);

    $("#recount").click();
    await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());
    expect(text()).not.toMatch(EXPECTED_TEXT);
    expect(text()).not.toContain("Beklenen");
  });
});
