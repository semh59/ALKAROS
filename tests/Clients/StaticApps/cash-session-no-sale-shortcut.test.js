import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/cash-session");
const TERMINAL = "11111111-1111-1111-1111-111111111111";
const SESSION = "22222222-2222-2222-2222-222222222222";
const ME = "33333333-3333-3333-3333-333333333333";

const session = { cashSessionId: SESSION, status: "Open", openingBalance: 1000, openedAt: "2026-11-18T08:00:00Z" };
const emptyList = { movements: [], movementsTruncated: false, requests: [], approvalThreshold: 500, selfApprovalEnabled: false };
const reasons = [{ code: "ChangeCoins", name: "Bozuk para" }, { code: "Other", name: "Diğer" }];
const drawer = (over = {}) => ({ enabled: true, canKick: true, noSaleCount: 0, warningCount: 5, reasons, ...over });
const reply = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body, headers: { get: () => null } });
const $ = (selector) => document.querySelector(selector);

async function openPage(query, world = {}) {
  const calls = [];
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    calls.push(`${method} ${url}`);
    if (url.endsWith("/auth/session/current")) return reply(200, { userId: ME, displayName: "Ayşe", terminalId: TERMINAL, capabilities: [] });
    if (url.endsWith("/cash-sessions/active")) return world.noSession ? reply(404, null) : reply(200, { ...session, status: world.sessionStatus ?? "Open", cashierUserId: world.owner ?? ME });
    if (url.endsWith("/suggested-opening-balance")) return reply(200, { suggestedOpeningBalance: null });
    if (url.endsWith("/cash-sessions/active/owner")) return reply(200, { displayName: "Başka Kasiyer" });
    if (url.endsWith("/cash-movements")) return reply(200, emptyList);
    if (url.endsWith("/cash-drawer") && method === "GET") {
      if (world.drawerDown) throw new TypeError("network");
      return reply(200, world.drawer ?? drawer());
    }
    throw new Error(`unrouted ${method} ${url}`);
  }));
  window.history.replaceState({}, "", `/${query}`);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "cash-session.js"));
  await vi.waitFor(() => expect(calls.some((c) => c.endsWith(world.noDrawerRead ? "/cash-sessions/active" : "/cash-drawer"))).toBe(true));
  await new Promise((done) => setTimeout(done, 30));
  return calls;
}

describe("cash-session.js opens the no-sale form when the sale screen asks for it", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => { vi.unstubAllGlobals(); document.body.innerHTML = ""; });

  it("shows the reason form at once, sends nothing, and the back link names the sale screen", async () => {
    const calls = await openPage("?returnTo=%2F&open=no-sale");

    expect($("#no-sale-form")).not.toBeNull();
    expect([...document.querySelectorAll("[data-no-sale-reason]")].map((chip) => chip.textContent)).toEqual(["Bozuk para", "Diğer"]);
    expect(calls.some((c) => c.includes("/no-sale"))).toBe(false);
    expect($(".cs-back").textContent).toBe("← Satış ekranına dön");
    expect($(".cs-back").getAttribute("href")).toBe("/");
  });

  it("leaves the normal page with its Satış yok button when nothing asks for the form", async () => {
    await openPage("?returnTo=%2F");

    expect($("#no-sale-form")).toBeNull();
    expect($("#no-sale")).not.toBeNull();
  });

  it("ignores any other value of open", async () => {
    await openPage("?returnTo=%2F&open=refund");

    expect($("#no-sale-form")).toBeNull();
  });

  it("opens the form only once: leaving it brings the normal page back, and a refresh does not open it again", async () => {
    const calls = await openPage("?returnTo=%2F&open=no-sale");

    $("#no-sale-cancel").click();
    await vi.waitFor(() => expect($("#no-sale")).not.toBeNull());
    $("#refresh-movements").click();
    await vi.waitFor(() => expect(calls.filter((c) => c === `GET /api/v1/terminals/${TERMINAL}/cash-sessions/${SESSION}/cash-drawer`)).toHaveLength(2));
    await new Promise((done) => setTimeout(done, 30));

    expect($("#no-sale-form")).toBeNull();
    expect($("#no-sale")).not.toBeNull();
  });

  it.each([
    ["the drawer is switched off", { drawer: drawer({ enabled: false }) }, "Bu kasada satışsız çekmece açma kapalı."],
    ["the user may not open the drawer", { drawer: drawer({ canKick: false }) }, "Satışsız çekmece açma yetkiniz yok."],
    ["the session belongs to someone else", { owner: "44444444-4444-4444-4444-444444444444" }, "Kasa oturumu başka kullanıcıda; satışsız çekmeceyi yalnız oturum sahibi açabilir."],
    ["the drawer status cannot be read", { drawerDown: true }, "Çekmece durumu okunamadı. Sayfayı yenileyip yeniden deneyin."],
    ["the count has started", { sessionStatus: "Counting", noDrawerRead: true }, "Kasa sayımı sürerken bu ekrandan satışsız çekmece açılamaz."],
    ["no session is open", { noSession: true, noDrawerRead: true }, "Satışsız çekmece açmak için önce kasa oturumu açın."],
  ])("says why in Turkish and shows no form when %s", async (_name, world, message) => {
    await openPage("?returnTo=%2F&open=no-sale", world);

    expect($("#no-sale-form")).toBeNull();
    expect(document.body.textContent).toContain(message);
  });

  it("takes the parameter out of the address once it was used, so a reload does not open the form again", async () => {
    await openPage("?returnTo=%2F&open=no-sale");

    expect(window.location.search).toBe("?returnTo=%2F");
  });
});
