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
const emptyList = { movements: [], movementsTruncated: false, requests: [], approvalThreshold: 500, selfApprovalEnabled: false };
const reply = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body, headers: { get: () => null } });
const text = () => document.body.textContent;
const title = () => document.querySelector(".cs-title").textContent;

/** A scripted terminal: `me` is signed in, the open drawer belongs to `owner`. */
function install({ ownerId = "u2", capabilities = [], status = "Open", ownerLookup = reply(200, { displayName: "Mehmet Usta" }), login = null }) {
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    if (url.endsWith("/auth/session/current")) {
      return login ? reply(401, {}) : reply(200, { userId: "u1", displayName: "Ayşe", terminalId: TERMINAL, capabilities });
    }
    if (url.endsWith("/auth/login")) return login;
    if (url.endsWith("/cash-sessions/active/owner")) return ownerLookup;
    if (url.endsWith("/cash-sessions/active")) {
      return reply(200, { cashSessionId: SESSION, cashierUserId: ownerId, status, openingBalance: 1000, openedAt: "2026-11-03T08:00:00Z" });
    }
    if (url.endsWith("/cash-movements") && method === "GET") return reply(200, emptyList);
    if (url.endsWith("/expected-cash")) return reply(200, { expectedCash: 1000, varianceTolerance: 50, openBillCount: 0, unsettledPaymentCount: 0 });
    throw new Error(`unrouted ${method} ${url}`);
  }));
}

async function open(world) {
  install(world);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect(document.querySelector(".cs-title")).not.toBeNull());
}

describe("cash-session.js names whose drawer session it is", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("titles the page with the cashier's own name, adds no notice and never asks who the owner is on their own session", async () => {
    await open({ ownerId: "u1" });

    expect(title()).toBe("Ayşe");
    expect(document.getElementById("owner-notice")).toBeNull();
    expect(document.getElementById("owner-hint")).toBeNull();
    expect(document.getElementById("start-count").disabled).toBe(false);
    expect(globalThis.fetch.mock.calls.some(([url]) => String(url).endsWith("/active/owner"))).toBe(false);
  });

  it("titles the page with the session owner's name, not the signed-in user's, and says who may close it", async () => {
    await open({});
    await vi.waitFor(() => expect(title()).toBe("Mehmet Usta"));

    const notice = document.getElementById("owner-notice").textContent;
    expect(notice).toContain("Mehmet Usta adına açık");
    expect(notice).toContain("oturumu açan kasiyer ya da bir yönetici");
    expect(text()).not.toContain("Ayşe");
  });

  it("keeps the count button off with the reason in the line right above it, for a cashier who is not the owner", async () => {
    await open({});
    await vi.waitFor(() => expect(title()).toBe("Mehmet Usta"));

    const button = document.getElementById("start-count");
    expect(button.disabled).toBe(true);
    expect(button.previousElementSibling.id).toBe("owner-hint");
    expect(button.previousElementSibling.textContent).toContain("Mehmet Usta ya da bir yönetici");
  });

  it("tells a manager they may close it for the owner and leaves the button on", async () => {
    await open({ capabilities: ["cash.session.override"] });
    await vi.waitFor(() => expect(title()).toBe("Mehmet Usta"));

    expect(document.getElementById("owner-notice").textContent).toContain("Yönetici olarak sizin de sayıp kapatma yetkiniz var");
    expect(document.getElementById("start-count").disabled).toBe(false);
    expect(document.getElementById("owner-hint")).toBeNull();
  });

  it.each([500, 404])("falls back to a plain phrase when the owner's name cannot be read (%i), and still explains", async (status) => {
    await open({ ownerLookup: reply(status, {}) });
    await vi.waitFor(() => expect(document.getElementById("owner-notice")).not.toBeNull());

    expect(title()).toBe("Başka bir kullanıcı");
    expect(document.getElementById("owner-notice").textContent).toContain("Başka bir kullanıcı adına açık");
  });

  it("holds the count save on the counting screen for a cashier who is not the owner, with the reason above it", async () => {
    await open({ status: "Counting" });
    await vi.waitFor(() => expect(document.getElementById("owner-notice")).not.toBeNull());

    const button = document.getElementById("submit-count");
    expect(button.disabled).toBe(true);
    expect(button.previousElementSibling.id).toBe("owner-hint");
  });

  it("lets a manager save the count of another cashier's drawer", async () => {
    await open({ status: "Counting", capabilities: ["cash.session.override"] });
    await vi.waitFor(() => expect(document.getElementById("owner-notice")).not.toBeNull());

    expect(document.getElementById("submit-count").disabled).toBe(false);
    expect(document.getElementById("owner-hint")).toBeNull();
  });

  it("answers a terminal bound to another browser with its own sentence, never with 'no permission'", async () => {
    install({ login: reply(403, { error: { code: "TERMINAL_KEY_MISMATCH", message: "raw server text" } }) });
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect(document.getElementById("login-form")).not.toBeNull());
    document.getElementById("username").value = "admin";
    document.getElementById("password").value = "x";
    document.getElementById("login-form").dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));

    await vi.waitFor(() => expect(document.querySelector(".cs-alert-body")).not.toBeNull());
    const message = document.querySelector(".cs-alert-body").textContent;
    expect(message).toContain("başka bir tarayıcıya bağlı");
    expect(message).not.toContain("yetkiniz yok");
    expect(message).not.toContain("raw server text");
  });
});
