import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/cash-session");
const TERMINAL = "11111111-1111-1111-1111-111111111111";
const session = { cashSessionId: "22222222-2222-2222-2222-222222222222", status: "Open", openingBalance: 0, openedAt: "2026-11-15T08:00:00Z" };
const emptyList = { movements: [], movementsTruncated: false, requests: [], approvalThreshold: 500, selfApprovalEnabled: false };
const reply = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body, headers: { get: () => null } });
const $ = (selector) => document.querySelector(selector);

const BILL = "/cashier/payments/split-payment/index.html?billId=abc";

async function openPageWith(returnTo) {
  const assign = vi.fn();
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    if (url.endsWith("/auth/session/current")) return reply(200, { userId: "u1", displayName: "Ayşe", terminalId: TERMINAL, capabilities: [] });
    if (url.endsWith("/cash-sessions/active")) return reply(404, null);
    if (url.endsWith("/suggested-opening-balance")) return reply(200, { suggestedOpeningBalance: null });
    if (url.endsWith("/cash-sessions") && method === "POST") return reply(201, session);
    if (url.endsWith("/cash-movements")) return reply(200, emptyList);
    throw new Error(`unrouted ${method} ${url}`);
  }));
  window.history.replaceState({}, "", returnTo === null ? "/" : `/?returnTo=${encodeURIComponent(returnTo)}`);
  vi.stubGlobal("location", { ...window.location, search: window.location.search, assign });
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "cash-session.js"));
  await vi.waitFor(() => expect($("#open-btn")).not.toBeNull());
  return assign;
}

async function openTheDrawer() {
  $("#open-btn").click();
  await vi.waitFor(() => expect(document.body.textContent).toContain("Açık"));
}

describe("cash-session.js returns to the bill the cashier came from", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => { vi.unstubAllGlobals(); document.body.innerHTML = ""; });

  it("turns the back link into the bill and goes there once the drawer is open", async () => {
    const assign = await openPageWith(BILL);
    expect($(".cs-back").textContent).toBe("← Hesaba dön");
    expect($(".cs-back").getAttribute("href")).toBe(BILL);
    $("#open-btn").click();
    await vi.waitFor(() => expect(assign).toHaveBeenCalledWith(BILL));
  });

  it.each([["/tables"], ["/tables?floor=2"], ["/tables#top"]])("names the tables when the cashier came from %s", async (tables) => {
    const assign = await openPageWith(tables);
    expect($(".cs-back").textContent).toBe("← Masalara dön");
    expect($(".cs-back").getAttribute("href")).toBe(tables);
    $("#open-btn").click();
    await vi.waitFor(() => expect(assign).toHaveBeenCalledWith(tables));
  });

  it("does not call another page the tables", async () => {
    await openPageWith("/tables-report");
    expect($(".cs-back").textContent).toBe("← Hesaba dön");
  });

  it.each(["//evil.example/x", "https://evil.example/x", "/" + String.fromCharCode(92) + "evil.example", "javascript:alert(1)", "bill", "/a b"])(
    "ignores the unsafe returnTo %s: no navigation and the plain back link",
    async (unsafe) => {
      const assign = await openPageWith(unsafe);
      expect($(".cs-back").textContent).toBe("← Kasaya dön");
      expect($(".cs-back").getAttribute("href")).toBe("/");
      await openTheDrawer();
      expect(assign).not.toHaveBeenCalled();
    },
  );

  it("stays on the page when there is no returnTo", async () => {
    const assign = await openPageWith(null);
    await openTheDrawer();
    expect(assign).not.toHaveBeenCalled();
  });
});
