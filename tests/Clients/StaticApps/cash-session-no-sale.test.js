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
const ME = "33333333-3333-3333-3333-333333333333";
const BASE = `/api/v1/terminals/${TERMINAL}/cash-sessions/${SESSION}`;

const session = { cashSessionId: SESSION, status: "Open", openingBalance: 1000, openedAt: "2026-11-17T08:00:00Z" };
const emptyList = { movements: [], movementsTruncated: false, requests: [], approvalThreshold: 500, selfApprovalEnabled: false };
const reasons = [{ code: "ChangeCoins", name: "Bozuk para" }, { code: "Count", name: "Sayım" }, { code: "Other", name: "Diğer" }];
const status = (over = {}) => ({ enabled: true, canKick: true, noSaleCount: 0, warningCount: 5, reasons, ...over });

const reply = (code, body) => ({ ok: code >= 200 && code < 300, status: code, json: async () => body, headers: { get: () => null } });
const $ = (selector) => document.querySelector(selector);
const text = () => document.body.textContent;

function install(world) {
  const calls = [];
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    calls.push({ url, method, body: init?.body ? JSON.parse(init.body) : null });
    if (url.endsWith("/auth/session/current")) return reply(200, { userId: ME, displayName: "Ayşe", terminalId: TERMINAL, capabilities: [] });
    if (url.endsWith("/cash-sessions/active")) return reply(200, { ...session, cashierUserId: world.owner ?? ME });
    if (url.endsWith("/cash-sessions/active/owner")) return reply(200, { displayName: "Başka Kasiyer" });
    if (url.endsWith("/cash-movements")) return reply(200, emptyList);
    if (url.endsWith("/cash-drawer") && method === "GET") {
      if (world.statusDown) throw new TypeError("network");
      return world.statusReply ?? reply(200, world.status ?? status());
    }
    if (url.endsWith("/cash-drawer/no-sale")) {
      if (world.networkDown) throw new TypeError("network");
      world.onPost?.();
      return world.postReplies?.shift() ?? reply(200, { delivered: true, noSaleCount: 1, warning: null, replayed: false });
    }
    throw new Error(`unrouted ${method} ${url}`);
  }));
  return calls;
}

async function open(world = {}) {
  const calls = install(world);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect($("#cash-out")).not.toBeNull());
  await vi.waitFor(() => expect(calls.some((c) => c.url.endsWith("/cash-drawer"))).toBe(true));
  await new Promise((done) => setTimeout(done, 20));
  return calls;
}

async function openForm(calls) {
  await vi.waitFor(() => expect($("#no-sale")).not.toBeNull());
  $("#no-sale").click();
  await vi.waitFor(() => expect($("#no-sale-form")).not.toBeNull());
  return calls;
}

const choose = (name) => [...document.querySelectorAll("[data-no-sale-reason]")].find((b) => b.textContent === name).click();
const type = (selector, value) => {
  const element = $(selector);
  element.value = value;
  element.dispatchEvent(new Event("input", { bubbles: true }));
};
const posts = (calls) => calls.filter((c) => c.method === "POST" && c.url.endsWith("/cash-drawer/no-sale"));

describe("cash-session.js no sale", () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, "", "/");
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("shows the button only when the drawer is on, the cashier may kick it and the session is their own", async () => {
    await open();
    expect($("#no-sale")?.textContent).toBe("Satış yok");
    document.body.innerHTML = "";
    vi.resetModules();

    for (const world of [{ status: status({ enabled: false }) }, { status: status({ canKick: false }) }, { statusDown: true }, { statusReply: reply(500, {}) }, { owner: "99999999-9999-9999-9999-999999999999" }]) {
      document.body.innerHTML = "";
      vi.resetModules();
      await open(world);
      expect($("#no-sale"), JSON.stringify(Object.keys(world))).toBeNull();
    }
  });

  it("reads the drawer status once from the session's own address", async () => {
    const calls = await open();
    expect(calls.filter((c) => c.url === `${BASE}/cash-drawer`)).toHaveLength(1);
  });

  it("offers the server's reasons and refuses to send without one", async () => {
    const calls = await openForm(await open());

    expect([...document.querySelectorAll("[data-no-sale-reason]")].map((b) => b.textContent)).toEqual(["Bozuk para", "Sayım", "Diğer"]);
    $("#no-sale-submit").click();
    await vi.waitFor(() => expect(text()).toContain("Bir gerekçe seçin."));
    expect(posts(calls)).toHaveLength(0);
  });

  it("needs a note for Diğer and keeps it to 200 characters", async () => {
    const calls = await openForm(await open());
    choose("Diğer");
    $("#no-sale-submit").click();
    await vi.waitFor(() => expect(text()).toContain("“Diğer” için not yazın."));

    expect($("#no-sale-note").getAttribute("maxlength")).toBe("200");
    type("#no-sale-note", "x".repeat(201));
    $("#no-sale-submit").click();
    await vi.waitFor(() => expect(text()).toContain("Not en çok 200 karakter olabilir."));
    expect(posts(calls)).toHaveLength(0);
  });

  it("opens the drawer with the chosen reason, then tells the cashier and counts the opening", async () => {
    const calls = await openForm(await open());
    choose("Bozuk para");
    $("#no-sale-submit").click();
    await vi.waitFor(() => expect(text()).toContain("Çekmece açıldı."));

    const [call] = posts(calls);
    expect(call.url).toBe(`${BASE}/cash-drawer/no-sale`);
    expect(call.body).toMatchObject({ ReasonCode: "ChangeCoins", Note: null });
    expect(call.body.IdempotencyKey).toMatch(/^[0-9a-f-]{36}$/);
    expect($("#no-sale-form")).toBeNull();
    expect(text()).toContain("Bu vardiyada satışsız açma: 1");
  });

  it("sends the note and keeps one request in flight on a double tap", async () => {
    let release;
    const world = { postReplies: [new Promise((resolve) => { release = resolve; })] };
    const calls = await openForm(await open(world));
    choose("Diğer");
    type("#no-sale-note", "  kasa tamiri ");
    $("#no-sale-submit").click();
    await vi.waitFor(() => expect(posts(calls)).toHaveLength(1));
    $("#no-sale-submit")?.click();

    release(reply(200, { delivered: true, noSaleCount: 1, warning: null, replayed: false }));
    await vi.waitFor(() => expect(text()).toContain("Çekmece açıldı."));
    expect(posts(calls)).toHaveLength(1);
    expect(posts(calls)[0].body).toMatchObject({ ReasonCode: "Other", Note: "kasa tamiri" });
  });

  it("says plainly when the drawer did not open, and shows the server's warning when there is one", async () => {
    const world = { postReplies: [reply(200, { delivered: false, noSaleCount: 5, warning: "Bu vardiyada 5 kez satışsız çekmece açıldı.", replayed: false })] };
    await openForm(await open(world));
    choose("Sayım");
    $("#no-sale-submit").click();

    await vi.waitFor(() => expect(text()).toContain("Çekmece açılamadı; yazıcı bağlantısını kontrol edin."));
    expect(text()).toContain("Bu vardiyada 5 kez satışsız çekmece açıldı.");
    expect(text()).not.toContain("Çekmece açıldı.");
  });

  it.each([
    [409, "CASH_DRAWER_DISABLED", "Para çekmecesi bağlantısı kapalı; yönetici Ayarlar ekranından açabilir."],
    [400, "CASH_DRAWER_REASON_INVALID", "Bir gerekçe seçin; “Diğer” için not yazın (en çok 200 karakter)."],
    [409, "CASH_DRAWER_SESSION_NOT_OPEN", "Kasa oturumu kapanmış; satışsız açma yalnız açık veya sayımdaki kasada yapılır."],
    [403, "FORBIDDEN", "Bu işlem için yetkiniz yok."],
    [500, "INTERNAL_ERROR", "Sunucu tarafında bir sorun oluştu. Lütfen tekrar deneyin."],
  ])("answers %i %s with a fixed Turkish sentence, never the server's text", async (code, errorCode, sentence) => {
    const world = { postReplies: [reply(code, { error: { code: errorCode, message: "RAW SERVER TEXT" } })] };
    await openForm(await open(world));
    choose("Sayım");
    $("#no-sale-submit").click();

    await vi.waitFor(() => expect(text()).toContain(sentence));
    expect(text()).not.toContain("RAW SERVER TEXT");
  });

  it("hides the button once the server says the drawer is switched off", async () => {
    const world = { postReplies: [reply(409, { error: { code: "CASH_DRAWER_DISABLED", message: "x" } })] };
    await openForm(await open(world));
    choose("Sayım");
    $("#no-sale-submit").click();
    await vi.waitFor(() => expect(text()).toContain("Para çekmecesi bağlantısı kapalı"));

    await vi.waitFor(() => expect($("#cash-in")).not.toBeNull());
    expect($("#no-sale")).toBeNull();
    expect($("#no-sale-form")).toBeNull();
    expect(text()).toContain("Para çekmecesi bağlantısı kapalı");
  });

  it("says on the form that the note is required once Diğer is chosen", async () => {
    await openForm(await open());
    expect($("label[for=no-sale-note]").textContent).toBe("Not");

    choose("Diğer");

    expect($("label[for=no-sale-note]").textContent).toBe("Not (zorunlu)");
  });

  it("leaves the form when the session has closed, and the refresh button reads the drawer again", async () => {
    const world = { postReplies: [reply(409, { error: { code: "CASH_DRAWER_SESSION_NOT_OPEN", message: "x" } })] };
    const calls = await openForm(await open(world));
    choose("Sayım");
    $("#no-sale-submit").click();
    await vi.waitFor(() => expect($("#no-sale-form")).toBeNull());
    expect($("#no-sale")).toBeNull();

    $("#refresh-movements").click();
    await vi.waitFor(() => expect(calls.filter((c) => c.url === `${BASE}/cash-drawer`)).toHaveLength(2));
    await vi.waitFor(() => expect($("#no-sale")).not.toBeNull());
  });

  it("gives every new opening of the form a new key, even after a lost connection", async () => {
    const calls = await openForm(await open({ networkDown: true }));
    choose("Sayım");
    $("#no-sale-submit").click();
    await vi.waitFor(() => expect(text()).toContain("Bağlantı kesildi."));
    $("#no-sale-cancel").click();
    await vi.waitFor(() => expect($("#no-sale")).not.toBeNull());
    $("#no-sale").click();
    await vi.waitFor(() => expect($("#no-sale-form")).not.toBeNull());
    choose("Sayım");
    $("#no-sale-submit").click();
    await vi.waitFor(() => expect(posts(calls)).toHaveLength(2));

    expect(posts(calls)[1].body.IdempotencyKey).not.toBe(posts(calls)[0].body.IdempotencyKey);
  });

  it("turns a line break in the note into a space, as the server refuses control characters", async () => {
    const calls = await openForm(await open());
    choose("Diğer");
    type("#no-sale-note", "kasa\ntamiri\t yapildi ");
    $("#no-sale-submit").click();
    await vi.waitFor(() => expect(posts(calls)).toHaveLength(1));

    expect(posts(calls)[0].body.Note).toBe("kasa tamiri yapildi");
  });

  it("starts on the reasons and keeps the focus on the reason just chosen", async () => {
    await openForm(await open());
    const chips = [...document.querySelectorAll("[data-no-sale-reason]")];
    expect(document.activeElement).toBe(chips[0]);

    chips[2].click();
    await vi.waitFor(() => expect(document.activeElement?.textContent).toBe("Diğer"));
    expect(document.activeElement.getAttribute("aria-pressed")).toBe("true");
  });

  it("retries a lost connection with the same key and uses a new key for a changed request", async () => {
    const world = { networkDown: true };
    const calls = await openForm(await open(world));
    choose("Sayım");
    $("#no-sale-submit").click();
    await vi.waitFor(() => expect(text()).toContain("Bağlantı kesildi. Çekmece açılmış olabilir"));
    $("#no-sale-submit").click();
    await vi.waitFor(() => expect(posts(calls)).toHaveLength(2));
    await vi.waitFor(() => expect($("#no-sale-submit").disabled).toBe(false));
    choose("Bozuk para");
    $("#no-sale-submit").click();
    await vi.waitFor(() => expect(posts(calls)).toHaveLength(3));

    const keys = posts(calls).map((c) => c.body.IdempotencyKey);
    expect(keys[1]).toBe(keys[0]);
    expect(keys[2]).not.toBe(keys[0]);
  });

  it("changes no amount on the screen: the opening balance stays as it was", async () => {
    await openForm(await open());
    choose("Sayım");
    $("#no-sale-submit").click();
    await vi.waitFor(() => expect(text()).toContain("Çekmece açıldı."));

    expect(text()).toContain("1.000,00");
  });
});
