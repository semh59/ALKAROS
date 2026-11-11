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
const OTHER = "44444444-4444-4444-4444-444444444444";
const REQUEST = "55555555-5555-5555-5555-555555555555";
const BASE = `/api/v1/terminals/${TERMINAL}/cash-sessions/${SESSION}`;

const session = { cashSessionId: SESSION, status: "Open", openingBalance: 1000, openedAt: "2026-11-03T08:00:00Z" };
const emptyList = { movements: [], movementsTruncated: false, requests: [], approvalThreshold: 500, selfApprovalEnabled: false };

/** A scripted fetch: `state` holds what each GET answers; every call is recorded; a handler may fail the network. */
function install(world) {
  const calls = [];
  const fetchMock = vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    calls.push({ url, method, body: init?.body ? JSON.parse(init.body) : null });
    const reply = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body, headers: { get: () => null } });
    if (url.endsWith("/auth/session/current")) {
      return reply(200, { userId: ME, displayName: "Ayşe", terminalId: TERMINAL, capabilities: world.capabilities ?? [] });
    }
    if (url.endsWith("/cash-sessions/active")) {
      if (world.activeFails) throw new TypeError("network");
      return reply(200, world.session ?? session);
    }
    if (url.endsWith("/expected-cash")) {
      if (world.expectedNetworkDown) throw new TypeError("network");
      if (world.expectedFails) return reply(500, { error: { code: "INTERNAL_ERROR", message: "boom" } });
      return reply(200, { expectedCash: world.expected ?? 1000 });
    }
    if (url.endsWith("/cash-movements") && method === "GET") {
      if (world.listFails) return reply(500, { error: { code: "INTERNAL_ERROR", message: "boom" } });
      return reply(200, world.list ?? emptyList);
    }
    if (url.endsWith("/cash-movements") && method === "POST") {
      if (world.networkDown) throw new TypeError("network");
      world.onPost?.();
      if (world.movementReplies?.length) return world.movementReplies.shift();
      return world.movementReply ?? reply(201, { id: "m1" });
    }
    if (url.includes("/cash-movement-requests/")) {
      if (world.decideNetworkDown) throw new TypeError("network");
      return world.decideReply ?? reply(200, { id: REQUEST });
    }
    if (url.endsWith("/start-count")) return world.startCountReply ?? reply(200, { ...session, status: "Counting" });
    if (url.endsWith("/counts")) return reply(200, {});
    if (url.endsWith("/close")) {
      if (world.closeNetworkDown) {
        world.activeFails = world.activeFailsAfterClose ?? false;
        throw new TypeError("network");
      }
      if (world.closeVariance) return reply(409, { error: { code: "CASH_VARIANCE_THRESHOLD_EXCEEDED", message: "RAW" } });
      return reply(200, { session: { ...session, closedAt: "2026-11-03T20:00:00Z" }, difference: 0 });
    }
    throw new Error(`unrouted ${method} ${url}`);
  });
  vi.stubGlobal("fetch", fetchMock);
  return calls;
}

const reply = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body, headers: { get: () => null } });
const $ = (selector) => document.querySelector(selector);
const text = () => document.body.textContent;

async function open(world = {}) {
  const calls = install(world);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect($("#cash-out")).not.toBeNull());
  return { calls, world };
}

async function type(selector, value) {
  const element = $(selector);
  element.value = value;
  element.dispatchEvent(new Event("input", { bubbles: true }));
}

async function startMovement(direction, amount, notes) {
  $(`#cash-${direction}`).click();
  await vi.waitFor(() => expect($("#movement-amount")).not.toBeNull());
  if (amount != null) await type("#movement-amount", amount);
  if (notes != null) await type("#movement-notes", notes);
}

async function toSummary(direction, amount, notes) {
  await startMovement(direction, amount, notes);
  $("#movement-submit").click();
  await vi.waitFor(() => expect($("#movement-confirm")).not.toBeNull());
}

const posted = (calls, suffix) => calls.filter((c) => c.method === "POST" && c.url.endsWith(suffix));

describe("cash-session.js cash movements", () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, "", "/");
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("has no in/out tabs: the direction is chosen once, by the button on the open session", async () => {
    await open();
    await startMovement("out");

    expect($(".cs-tabs")).toBeNull();
    expect($("#tab-in")).toBeNull();
    expect($("h1").textContent).toBe("Nakit Çıkış");
  });

  it("refuses an empty reason before anything reaches the server", async () => {
    const { calls } = await open();
    await startMovement("out", "40", "   ");

    $("#movement-submit").click();
    await vi.waitFor(() => expect(text()).toContain("Açıklama zorunludur."));

    expect(posted(calls, "/cash-movements")).toHaveLength(0);
    expect(calls.some((c) => c.url.endsWith("/expected-cash"))).toBe(false);
    expect($("label[for=movement-notes]").textContent).toBe("Açıklama");
  });

  it("refuses a zero amount and a reason over 500 characters", async () => {
    const { calls } = await open();
    await startMovement("out", "0", "gider");
    $("#movement-submit").click();
    await vi.waitFor(() => expect(text()).toContain("Tutar sıfırdan büyük olmalı."));

    await type("#movement-amount", "10");
    await type("#movement-notes", "x".repeat(501));
    $("#movement-submit").click();
    await vi.waitFor(() => expect(text()).toContain("Açıklama en çok 500 karakter olabilir."));
    expect(posted(calls, "/cash-movements")).toHaveLength(0);
  });

  it("shows a summary of direction, amount, reason and expected cash before saving", async () => {
    const { calls } = await open({ expected: 1200 });
    await toSummary("out", "40", "  market  ");

    const summary = text();
    expect(summary).toContain("Nakit Çıkış");
    expect(summary).toContain("40,00");
    expect(summary).toContain("market");
    expect(summary).toContain("1.200,00");
    expect($("#approval-warning")).toBeNull();
    expect(posted(calls, "/cash-movements")).toHaveLength(0);
  });

  it("warns that a manager must approve a cash-out above the threshold, and not a cash-in", async () => {
    await open({ expected: 5000 });
    await toSummary("out", "800", "avans");
    expect($("#approval-warning").textContent).toContain("Yönetici onayı gerekecek");

    $("#movement-edit").click();
    await vi.waitFor(() => expect($("#movement-amount")).not.toBeNull());
    expect($("#movement-amount").value).toBe("800");
    expect($("#movement-notes").value).toBe("avans");
  });

  it("does not warn at exactly the threshold", async () => {
    await open({ expected: 5000 });
    await toSummary("out", "500", "avans");
    expect($("#approval-warning")).toBeNull();
  });

  it("blocks saving a cash-out that is more than the drawer holds", async () => {
    await open({ expected: 100 });
    await toSummary("out", "150", "fazla");
    expect($("#drawer-warning")).not.toBeNull();
    expect($("#movement-confirm").disabled).toBe(true);
  });

  it("sends the trimmed reason on confirmation and announces a saved movement, then lists it", async () => {
    const list = {
      ...emptyList,
      movements: [{ id: "m1", type: "CashOut", direction: "Out", amount: 40, notes: "market", recordedBy: ME, recordedByName: "Ayşe", occurredAt: "2026-11-03T09:30:00Z", approvedBy: null, approvedByName: null, selfApproved: false }],
    };
    const world = { list: emptyList, onPost: () => { world.list = list; } };
    const { calls } = await open(world);
    expect($("#movement-list").textContent).not.toContain("market");
    await toSummary("out", "40", "  market ");

    $("#movement-confirm").click();
    await vi.waitFor(() => expect($("#cs-notice")).not.toBeNull());

    expect(posted(calls, "/cash-movements")[0].body).toMatchObject({ Direction: "Out", Amount: 40, Notes: "market" });
    expect($("#cs-notice").textContent).toBe("Çıkış kaydedildi (₺40,00).");
    expect($("#movement-list").textContent).toContain("market");
    expect($("#movement-list").textContent).toContain("Ayşe");
  });

  it("says the cash-out waits for a manager when the server answers 202", async () => {
    await open({ movementReply: reply(202, { id: REQUEST, status: "Requested" }) });
    await toSummary("out", "800", "avans");

    $("#movement-confirm").click();
    await vi.waitFor(() => expect($("#cs-notice")).not.toBeNull());

    expect($("#cs-notice").textContent).toContain("yönetici onayı bekliyor");
  });

  it("lists who approved a movement and marks a self-approval", async () => {
    const entry = (approvedByName, selfApproved) => ({ id: approvedByName, type: "CashOut", direction: "Out", amount: 800, notes: "avans", recordedBy: ME, recordedByName: "Ayşe", occurredAt: "2026-11-03T09:30:00Z", approvedBy: OTHER, approvedByName, selfApproved });
    await open({ list: { ...emptyList, movements: [entry("Mehmet", false), entry("Ayşe", true)] } });

    const list = $("#movement-list").textContent;
    expect(list).toContain("onaylayan: Mehmet");
    expect(list).toContain("kendi onayı");
  });

  it("keeps the typed amount and reason when the server refuses, with the Turkish text and never the raw message", async () => {
    const refusal = reply(409, { error: { code: "CASH_OUT_EXCEEDS_DRAWER", message: "RAW ENGLISH MESSAGE" } });
    await open({ expected: 5000, movementReply: refusal });
    await toSummary("out", "40", "market");

    $("#movement-confirm").click();
    await vi.waitFor(() => expect($("#movement-amount")).not.toBeNull());

    expect(text()).toContain("Çıkış tutarı kasadaki nakdi aşıyor.");
    expect(text()).not.toContain("RAW ENGLISH MESSAGE");
    expect($("#movement-amount").value).toBe("40");
    expect($("#movement-notes").value).toBe("market");
  });

  it("keeps what was typed through a validation re-render", async () => {
    await open();
    await startMovement("in", "25", "");
    $("#movement-submit").click();
    await vi.waitFor(() => expect(text()).toContain("Açıklama zorunludur."));

    expect($("#movement-amount").value).toBe("25");
  });

  it("reuses the retry key for the same body after a network failure and mints a new one when the reason changes", async () => {
    const world = { networkDown: true };
    const { calls } = await open(world);
    await toSummary("out", "40", "market");

    $("#movement-confirm").click();
    await vi.waitFor(() => expect(text()).toContain("Sunucuya ulaşılamadı."));
    await vi.waitFor(() => expect($("#movement-confirm")).not.toBeNull());
    $("#movement-confirm").click();
    await vi.waitFor(() => expect(posted(calls, "/cash-movements")).toHaveLength(2));
    $("#movement-edit").click();
    await vi.waitFor(() => expect($("#movement-notes")).not.toBeNull());
    await type("#movement-notes", "market 2");
    $("#movement-submit").click();
    await vi.waitFor(() => expect($("#movement-confirm")).not.toBeNull());
    $("#movement-confirm").click();
    await vi.waitFor(() => expect(posted(calls, "/cash-movements")).toHaveLength(3));

    const keys = posted(calls, "/cash-movements").map((c) => c.body.IdempotencyKey);
    expect(keys[1]).toBe(keys[0]);
    expect(keys[2]).not.toBe(keys[0]);
  });
});

describe("cash-session.js cash-out requests", () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, "", "/");
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  const request = (requestedBy, selfApprovalEnabled = false) => ({
    ...emptyList,
    selfApprovalEnabled,
    requests: [{ id: REQUEST, amount: 800, reason: "avans", requestedBy, requestedByName: "Talep Eden", requestedAt: "2026-11-03T09:00:00Z" }],
  });
  const buttons = () => [...document.querySelectorAll("[data-request-action]")].map((b) => b.dataset.requestAction);

  it("lets an approver approve and reject another person's request, and not cancel it", async () => {
    await open({ capabilities: ["cash.session.override"], list: request(OTHER) });
    expect(buttons()).toEqual(["approve", "reject"]);
    expect($("#pending-requests").textContent).toContain("avans");
  });

  it("withholds Onayla from a manager's own request while self-approval is off, and says why", async () => {
    await open({ capabilities: ["cash.session.override"], list: request(ME) });
    expect(buttons()).toEqual(["reject", "cancel"]);
    expect($("#pending-requests").textContent).toContain("Kendi talebinizi onaylayamazsınız");
  });

  it("offers Onayla on a manager's own request once self-approval is on", async () => {
    await open({ capabilities: ["cash.session.override"], list: request(ME, true) });
    expect(buttons()).toEqual(["approve", "reject", "cancel"]);
  });

  it("asks once more before approving or rejecting, says what will happen, and sends nothing on Hayır", async () => {
    const { calls } = await open({ capabilities: ["cash.session.override"], list: request(OTHER) });

    document.querySelector("[data-request-action=approve]").click();
    await vi.waitFor(() => expect($("#request-confirm-text")).not.toBeNull());
    expect($("#request-confirm-text").textContent).toContain("₺800,00 kasadan düşülecek");
    $("#request-back").click();
    await vi.waitFor(() => expect($("[data-request-action=approve]")).not.toBeNull());
    document.querySelector("[data-request-action=reject]").click();
    await vi.waitFor(() => expect($("#request-confirm-text")).not.toBeNull());

    expect($("#request-confirm-text").textContent).toContain("geri alınamaz");
    expect(calls.some((c) => c.url.includes("/cash-movement-requests/"))).toBe(false);
  });

  it("names the amount and the requester on each request button for a screen reader", async () => {
    await open({ capabilities: ["cash.session.override"], list: request(OTHER) });
    expect(document.querySelector("[data-request-action=approve]").getAttribute("aria-label")).toBe("Onayla: ₺800,00, Talep Eden");
  });

  it("blocks Sayıma Başla while a request waits and says why", async () => {
    await open({ capabilities: [], list: request(ME) });
    expect($("#start-count").disabled).toBe(true);
    expect($("#pending-hint").textContent).toContain("sayıma geçilemez");
    expect($("#count-hint")).toBeNull();
  });

  it("names the count button as the first step of closing the drawer", async () => {
    await open();

    expect($("#start-count").textContent).toBe("Kasayı Kapat: Sayıma Başla");
    expect($("#count-hint").textContent).toContain("başlatınca geri dönülmez");
    expect($("#pending-hint")).toBeNull();
  });

  it("starts with an enabled Sayıma Başla when nothing waits", async () => {
    await open();
    expect($("#start-count").disabled).toBe(false);
    expect($("#pending-hint")).toBeNull();
  });

  it("reloads the list on Listeyi yenile", async () => {
    const { calls } = await open();
    const reads = () => calls.filter((c) => c.method === "GET" && c.url.endsWith("/cash-movements")).length;
    const before = reads();
    $("#refresh-movements").click();
    await vi.waitFor(() => expect(reads()).toBe(before + 1));
  });

  it("gives the requester without approval permission only Vazgeç", async () => {
    await open({ capabilities: [], list: request(ME) });
    expect(buttons()).toEqual(["cancel"]);
  });

  it("shows nothing to a user who neither asked nor may approve", async () => {
    await open({ capabilities: [], list: request(OTHER) });
    expect(buttons()).toEqual([]);
  });

  it.each([
    ["approve", "Çıkış onaylandı ve kasadan düşüldü."],
    ["reject", "Çıkış talebi reddedildi."],
    ["cancel", "Çıkış talebi geri çekildi."],
  ])("posts %s to the request and reloads the list", async (action, notice) => {
    const { calls } = await open({ capabilities: ["cash.session.override"], list: request(ME, true) });

    document.querySelector(`[data-request-action=${action}]`).click();
    if (action !== "cancel") {
      await vi.waitFor(() => expect($("[data-request-confirm]")).not.toBeNull());
      $("[data-request-confirm]").click();
    }
    await vi.waitFor(() => expect($("#cs-notice")).not.toBeNull());

    expect(posted(calls, `/cash-movement-requests/${REQUEST}/${action}`)).toHaveLength(1);
    expect($("#cs-notice").textContent).toBe(notice);
  });

  it("is told in Turkish when the request was already decided", async () => {
    const calls = install({ capabilities: ["cash.session.override"], list: request(OTHER) });
    const original = globalThis.fetch;
    vi.stubGlobal("fetch", vi.fn(async (url, init) => (url.includes("/cash-movement-requests/")
      ? reply(409, { error: { code: "CASH_MOVEMENT_REQUEST_STATE", message: "RAW" } })
      : original(url, init))));
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("[data-request-action=approve]")).not.toBeNull());

    $("[data-request-action=approve]").click();
    await vi.waitFor(() => expect($("[data-request-confirm]")).not.toBeNull());
    $("[data-request-confirm]").click();
    await vi.waitFor(() => expect(text()).toContain("artık beklemiyor"));

    expect(text()).toContain("liste yenilendi");
    expect(text()).not.toContain("RAW");
    expect($("#cs-notice")).toBeNull();
    expect(calls.filter((c) => c.url.includes("/cash-movement-requests/"))).toHaveLength(0);
  });
});

describe("cash-session.js counting and closing", () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, "", "/");
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  async function openCounting(world = {}) {
    const result = await open(world);
    $("#start-count").click();
    await vi.waitFor(() => expect($("#submit-count")).not.toBeNull());
    return result;
  }

  it("asks whether the drawer is empty before accepting a zero count, and does not send until confirmed", async () => {
    const { calls } = await openCounting();

    $("#submit-count").click();
    await vi.waitFor(() => expect($("#zero-count-prompt")).not.toBeNull());

    expect($("#zero-count-prompt").textContent).toContain("Sayım 0 ₺ - çekmece boş mu?");
    expect(posted(calls, "/counts")).toHaveLength(0);

    $("#zero-confirm").click();
    await vi.waitFor(() => expect(posted(calls, "/counts")).toHaveLength(1));
    expect(posted(calls, "/counts")[0].body.CountedAmount).toBe(0);
  });

  it("lets the cashier go back from the zero-count question to correct the count", async () => {
    const { calls } = await openCounting();
    $("#submit-count").click();
    await vi.waitFor(() => expect($("#zero-cancel")).not.toBeNull());

    $("#zero-cancel").click();
    await vi.waitFor(() => expect($("#submit-count")).not.toBeNull());

    expect($("#zero-count-prompt")).toBeNull();
    expect(posted(calls, "/counts")).toHaveLength(0);
  });

  it("does not ask about a count above zero, in either mode", async () => {
    const { calls } = await openCounting();
    await type("#denom-100", "2");
    $("#submit-count").click();
    await vi.waitFor(() => expect(posted(calls, "/counts")).toHaveLength(1));
    expect(posted(calls, "/counts")[0].body.CountedAmount).toBe(200);
  });

  it("also asks for a single typed amount of 0", async () => {
    const { calls } = await openCounting();
    $("#toggle-count-mode").click();
    await vi.waitFor(() => expect($("#counted-amount")).not.toBeNull());
    await type("#counted-amount", "0");

    $("#submit-count").click();
    await vi.waitFor(() => expect($("#zero-count-prompt")).not.toBeNull());
    expect(posted(calls, "/counts")).toHaveLength(0);
  });

  it("keeps the typed count and note through an error re-render", async () => {
    const { calls } = await openCounting();
    $("#toggle-count-mode").click();
    await vi.waitFor(() => expect($("#counted-amount")).not.toBeNull());
    await type("#counted-amount", "-5");
    await type("#count-notes", "not");

    $("#submit-count").click();
    await vi.waitFor(() => expect(text()).toContain("Geçerli bir tutar girin."));

    expect($("#counted-amount").value).toBe("-5");
    expect($("#count-notes").value).toBe("not");
    expect(posted(calls, "/counts")).toHaveLength(0);
  });

  async function toClosing(world) {
    const result = await openCounting(world);
    await type("#denom-100", "10");
    $("#submit-count").click();
    await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());
    return result;
  }

  it("disables closing and offers a re-read when the expected cash could not be read", async () => {
    const world = { expectedFails: true };
    const { calls } = await toClosing(world);

    expect($("#submit-close").disabled).toBe(true);
    expect($("#expected-missing")).not.toBeNull();

    world.expectedFails = false;
    $("#reread-expected").click();
    await vi.waitFor(() => expect($("#submit-close").disabled).toBe(false));

    expect($("#reread-expected")).toBeNull();
    expect(calls.filter((c) => c.url.endsWith("/expected-cash")).length).toBe(2);
  });

  it("tells the cashier the session may have closed after a dropped connection and reads the state again", async () => {
    const world = { closeNetworkDown: true };
    const { calls } = await toClosing(world);
    const activeReads = () => calls.filter((c) => c.url.endsWith("/cash-sessions/active")).length;
    const before = activeReads();

    $("#submit-close").click();
    await vi.waitFor(() => expect($("#cs-notice")).not.toBeNull());

    expect($("#cs-notice").textContent).toContain("kasa kapanmış olabilir");
    expect(activeReads()).toBe(before + 1);
  });

  it("keeps the supervisor explanation through a re-render and shows the Turkish variance text", async () => {
    const world = { closeVariance: true };
    const { calls } = await toClosing(world);

    $("#submit-close").click();
    await vi.waitFor(() => expect($("#override-reason")).not.toBeNull());
    expect(text()).toContain("Fark tolerans sınırını aşıyor");
    expect(text()).not.toContain("RAW");
    await type("#override-reason", "sebep");
    $("#submit-close").click();
    await vi.waitFor(() => expect(posted(calls, "/close")).toHaveLength(2));
    await vi.waitFor(() => expect($("#submit-close").disabled).toBe(false));

    expect($("#override-reason").value).toBe("sebep");
    expect(posted(calls, "/close")[1].body.OverrideReason).toBe("sebep");
  });
});

const openFormAndSubmit = async (direction, amount, notes) => {
  await startMovement(direction, amount, notes);
  $("#movement-submit").click();
};

describe("cash-session.js movement flow details", () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, "", "/");
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  const confirm = async (calls, count) => {
    await vi.waitFor(() => expect($("#movement-confirm")).not.toBeNull());
    $("#movement-confirm").click();
    await vi.waitFor(() => expect(posted(calls, "/cash-movements")).toHaveLength(count));
  };
  const keysOf = (calls) => posted(calls, "/cash-movements").map((c) => c.body.IdempotencyKey);

  it("treats a cash-in of any size as plain: no approval warning, no drawer limit, Direction In, own label", async () => {
    const { calls } = await open({ expected: 100 });
    await toSummary("in", "800", "takviye");

    expect($("#approval-warning")).toBeNull();
    expect($("#drawer-warning")).toBeNull();
    expect($("#movement-confirm").textContent).toBe("Onayla ve kaydet");
    await confirm(calls, 1);
    expect(posted(calls, "/cash-movements")[0].body.Direction).toBe("In");
    await vi.waitFor(() => expect($("#cs-notice")).not.toBeNull());
    expect($("#cs-notice").textContent).toBe("Giriş kaydedildi (₺800,00).");
  });

  it("labels the button Onaya gönder when a manager has to approve", async () => {
    await open({ expected: 5000 });
    await toSummary("out", "800", "avans");
    expect($("#movement-confirm").textContent).toBe("Onaya gönder");
  });

  it("mints a new retry key after the server decided (409), keeps it after a 500", async () => {
    const world = { movementReplies: [reply(409, { error: { code: "CASH_OUT_EXCEEDS_DRAWER", message: "x" } }), reply(500, { error: { code: "INTERNAL_ERROR", message: "x" } }), reply(500, { error: { code: "INTERNAL_ERROR", message: "x" } })] };
    const { calls } = await open(world);

    await openFormAndSubmit("out", "40", "market");
    await confirm(calls, 1);
    await vi.waitFor(() => expect($("#movement-submit")).not.toBeNull());
    $("#movement-submit").click();
    await confirm(calls, 2);
    await vi.waitFor(() => expect($("#movement-submit")).not.toBeNull());
    $("#movement-submit").click();
    await confirm(calls, 3);

    const keys = keysOf(calls);
    expect(keys[1]).not.toBe(keys[0]);
    expect(keys[2]).toBe(keys[1]);
  });

  it("mints a new key when the amount changes or the direction changes", async () => {
    const world = { networkDown: true };
    const { calls } = await open(world);
    await openFormAndSubmit("out", "40", "market");
    await confirm(calls, 1);
    await vi.waitFor(() => expect($("#movement-edit")).not.toBeNull());
    $("#movement-edit").click();
    await vi.waitFor(() => expect($("#movement-amount")).not.toBeNull());
    await type("#movement-amount", "41");
    $("#movement-submit").click();
    await confirm(calls, 2);
    $("#movement-edit").click();
    await vi.waitFor(() => expect($("#movement-cancel")).not.toBeNull());
    $("#movement-cancel").click();
    await vi.waitFor(() => expect($("#cash-in")).not.toBeNull());
    await openFormAndSubmit("in", "41", "market");
    await confirm(calls, 3);

    const keys = keysOf(calls);
    expect(new Set(keys).size).toBe(3);
  });

  it.each([
    ["CASH_MOVEMENT_REASON_INVALID", "Açıklama zorunludur ve en çok 500"],
    ["CASH_MOVEMENT_REQUESTS_PENDING", "Onay bekleyen nakit çıkış talebi var"],
    ["CASH_MOVEMENT_SELF_APPROVAL", "Kendi çıkış talebinizi onaylayamazsınız"],
    ["IDEMPOTENCY_KEY_REUSED", "zaten kaydedilmiş"],
    ["CLOSED_CASH_SESSION", "Kasa oturumu açık değil"],
  ])("shows the Turkish text of %s and never the raw server message", async (code, expected) => {
    await open({ movementReply: reply(409, { error: { code, message: "RAW ENGLISH" } }) });
    await toSummary("out", "40", "market");

    $("#movement-confirm").click();
    await vi.waitFor(() => expect($("#movement-submit")).not.toBeNull());

    expect(text()).toContain(expected);
    expect(text()).not.toContain("RAW ENGLISH");
  });

  it("shows the Turkish pending-request text when the count cannot start", async () => {
    await open({ startCountReply: reply(409, { error: { code: "CASH_MOVEMENT_REQUESTS_PENDING", message: "RAW" } }) });
    $("#start-count").click();
    await vi.waitFor(() => expect(text()).toContain("Onay bekleyen nakit çıkış talebi var"));
    expect(text()).not.toContain("RAW");
  });

  it("says the movement may have been saved after a dropped connection", async () => {
    await open({ networkDown: true });
    await toSummary("out", "40", "market");
    $("#movement-confirm").click();
    await vi.waitFor(() => expect(text()).toContain("Kayıt gitmiş olabilir"));
    expect($("#movement-confirm")).not.toBeNull();
  });

  it("explains a replay that was decided earlier instead of claiming a new save", async () => {
    await open({ movementReply: reply(200, { id: REQUEST, status: "Rejected" }) });
    await toSummary("out", "800", "avans");
    $("#movement-confirm").click();
    await vi.waitFor(() => expect($("#cs-notice")).not.toBeNull());
    expect($("#cs-notice").textContent).toContain("daha önce reddedilmişti");
  });

  it("accepts a reason of exactly 500 characters", async () => {
    await open();
    await toSummary("out", "40", "x".repeat(500));
    expect($("#movement-confirm")).not.toBeNull();
  });

  it("escapes what the cashier typed in the summary, the list and the request panel", async () => {
    const hostile = '<img src=x onerror="alert(1)"><b>x</b>';
    const list = {
      ...emptyList,
      movements: [{ id: "m", type: "CashOut", direction: "Out", amount: 5, notes: hostile, recordedBy: ME, recordedByName: hostile, occurredAt: "2026-11-03T09:30:00Z", approvedBy: null, approvedByName: null, selfApproved: false }],
      requests: [{ id: REQUEST, amount: 800, reason: hostile, requestedBy: OTHER, requestedByName: hostile, requestedAt: "2026-11-03T09:00:00Z" }],
    };
    await open({ capabilities: ["cash.session.override"], list });
    expect(document.querySelector("#movement-list img, #pending-requests img, #movement-list b, #pending-requests b")).toBeNull();

    await toSummary("out", "40", hostile);
    expect(document.querySelector("img, b")).toBeNull();
    expect(text()).toContain("<img src=x");
  });

  it("shows when the list was cut and when it could not be read, without blocking the screen", async () => {
    const cut = await open({ list: { ...emptyList, movements: [{ id: "m", type: "CashIn", direction: "In", amount: 5, notes: "a", recordedBy: ME, recordedByName: "Ayşe", occurredAt: "2026-11-03T09:30:00Z", approvedBy: null, approvedByName: null, selfApproved: false }], movementsTruncated: true } });
    expect($("#movement-list").textContent).toContain("Yalnız son 200 hareket gösteriliyor.");
    expect(cut.calls.length).toBeGreaterThan(0);
    document.body.innerHTML = "";
    vi.resetModules();

    await open({ listFails: true });
    expect($("#movement-list-error").textContent).toBe("Nakit hareketleri okunamadı.");
    expect($("#cash-in")).not.toBeNull();
    expect($("#start-count").disabled).toBe(false);
  });

  it("shows each movement with its direction, sign, amount, time and reason", async () => {
    const entry = (id, direction, notes) => ({ id, type: direction === "In" ? "CashIn" : "CashOut", direction, amount: 40, notes, recordedBy: ME, recordedByName: "Ayşe", occurredAt: "2026-11-03T09:30:00Z", approvedBy: null, approvedByName: null, selfApproved: false });
    await open({ list: { ...emptyList, movements: [entry("a", "Out", "market"), entry("b", "In", "takviye")] } });
    const rows = [...document.querySelectorAll(".cs-movement")].map((row) => row.textContent);
    expect(rows[0]).toContain("12:30 · Çıkış-₺40,00market");
    expect(rows[1]).toContain("12:30 · Giriş+₺40,00takviye");
    expect(document.querySelector(".cs-movement.is-out")).not.toBeNull();
    expect(document.querySelector(".cs-movement.is-in")).not.toBeNull();
  });

  it("tells the cashier when a decision may not have arrived", async () => {
    await open({ capabilities: ["cash.session.override"], list: { ...emptyList, requests: [{ id: REQUEST, amount: 800, reason: "avans", requestedBy: ME, requestedByName: "A", requestedAt: "2026-11-03T09:00:00Z" }] }, decideNetworkDown: true });
    document.querySelector("[data-request-action=cancel]").click();
    await vi.waitFor(() => expect(text()).toContain("Karar gitmiş olabilir"));
    expect($("#cs-notice")).toBeNull();
  });

  it.each(["reject", "cancel"])("shows the Turkish already-decided text when %s finds the request closed", async (action) => {
    await open({
      capabilities: ["cash.session.override"],
      list: { ...emptyList, requests: [{ id: REQUEST, amount: 800, reason: "avans", requestedBy: ME, requestedByName: "A", requestedAt: "2026-11-03T09:00:00Z" }] },
      decideReply: reply(409, { error: { code: "CASH_MOVEMENT_REQUEST_STATE", message: "RAW" } }),
    });
    document.querySelector(`[data-request-action=${action}]`).click();
    if (action !== "cancel") {
      await vi.waitFor(() => expect($("[data-request-confirm]")).not.toBeNull());
      $("[data-request-confirm]").click();
    }
    await vi.waitFor(() => expect(text()).toContain("artık beklemiyor"));
    expect(text()).not.toContain("RAW");
  });

  it("clears the typed movement after a save and on Vazgeç, so the next one starts empty", async () => {
    const { calls } = await open();
    await toSummary("out", "40", "market");
    await confirm(calls, 1);
    await vi.waitFor(() => expect($("#cs-notice")).not.toBeNull());
    await startMovement("out");
    expect($("#movement-amount").value).toBe("");
    expect($("#movement-notes").value).toBe("");

    await type("#movement-amount", "7");
    await type("#movement-notes", "kalem");
    $("#movement-cancel").click();
    await vi.waitFor(() => expect($("#cash-out")).not.toBeNull());
    await startMovement("out");
    expect($("#movement-amount").value).toBe("");
    expect($("#movement-notes").value).toBe("");
  });

  it("fills the reason from a quick choice and sends it", async () => {
    const { calls } = await open();
    await startMovement("out", "40");
    document.querySelector('[data-reason="Banka"]').click();
    expect($("#movement-notes").value).toBe("Banka");
    $("#movement-form").requestSubmit();
    await confirm(calls, 1);
    expect(posted(calls, "/cash-movements")[0].body.Notes).toBe("Banka");
  });

  it("moves focus to the new step and to an error", async () => {
    await open();
    await startMovement("out", "40", "");
    $("#movement-submit").click();
    await vi.waitFor(() => expect($(".cs-alert-danger")).not.toBeNull());
    expect(document.activeElement).toBe($(".cs-alert-danger"));

    await type("#movement-notes", "market");
    $("#movement-submit").click();
    await vi.waitFor(() => expect($("#movement-confirm")).not.toBeNull());
    expect(document.activeElement.id).toBe("movement-edit");
  });

  it("does not move focus when the same screen is only redrawn", async () => {
    await open();
    expect(document.activeElement).toBe($("#cash-in"));

    $("#refresh-movements").click();
    await vi.waitFor(() => expect($("#refresh-movements").disabled).toBe(false));

    expect(document.activeElement).toBe(document.body);
  });

  it("starts the next shift clean: no old list, requests, counts or drafts", async () => {
    const world = { expected: 300, list: { ...emptyList, movements: [{ id: "old", type: "CashIn", direction: "In", amount: 5, notes: "eski vardiya", recordedBy: ME, recordedByName: "Ayşe", occurredAt: "2026-11-03T09:30:00Z", approvedBy: null, approvedByName: null, selfApproved: false }] } };
    await open(world);
    expect($("#movement-list").textContent).toContain("eski vardiya");
    $("#start-count").click();
    await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());
    await type("#denom-100", "3");
    await type("#count-notes", "eski not");
    $("#submit-count").click();
    await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());
    $("#submit-close").click();
    await vi.waitFor(() => expect($("#reopen")).not.toBeNull());

    world.list = emptyList;
    $("#reopen").click();
    await vi.waitFor(() => expect($("#cash-out")).not.toBeNull());
    expect(text()).not.toContain("eski vardiya");
    $("#start-count").click();
    await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());

    expect($("#denom-100").value).toBe("0");
    expect($("#count-notes").value).toBe("");
    expect($("#denom-total-value").textContent).toBe("₺0,00");
  });
});

describe("cash-session.js counting and closing details", () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, "", "/");
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  async function openCounting(world = {}) {
    const result = await open(world);
    $("#start-count").click();
    await vi.waitFor(() => expect($("#submit-count")).not.toBeNull());
    return result;
  }

  it("locks the count fields while the zero-count question is open, and keeps the typed note when the cashier goes back", async () => {
    await openCounting();
    await type("#count-notes", "sayım notu");
    $("#submit-count").click();
    await vi.waitFor(() => expect($("#zero-count-prompt")).not.toBeNull());

    expect($("#denom-100").disabled).toBe(true);
    expect($("#count-notes").disabled).toBe(true);
    expect($("#toggle-count-mode").disabled).toBe(true);

    $("#zero-cancel").click();
    await vi.waitFor(() => expect($("#submit-count")).not.toBeNull());
    expect($("#count-notes").value).toBe("sayım notu");
    expect($("#denom-100").disabled).toBe(false);
  });

  it("keeps the typed count through a switch between kupür and single amount", async () => {
    await openCounting();
    await type("#count-notes", "not");
    $("#toggle-count-mode").click();
    await vi.waitFor(() => expect($("#counted-amount")).not.toBeNull());

    expect($("#count-notes").value).toBe("not");
  });

  it("goes on to the close screen with a re-read button when the expected cash fails on the network after the count", async () => {
    const { calls } = await openCounting({ expectedNetworkDown: true });
    await type("#denom-100", "2");
    $("#submit-count").click();
    await vi.waitFor(() => expect($("#reread-expected")).not.toBeNull());

    expect($("#submit-close").disabled).toBe(true);
    expect(posted(calls, "/counts")).toHaveLength(1);
  });

  it("says so again when the re-read fails too, and keeps closing disabled", async () => {
    const world = { expectedFails: true };
    await openCounting(world);
    await type("#denom-100", "2");
    $("#submit-count").click();
    await vi.waitFor(() => expect($("#reread-expected")).not.toBeNull());

    $("#reread-expected").click();
    await vi.waitFor(() => expect(text()).toContain("Beklenen tutar okunamadı. Bağlantınızı kontrol edip tekrar deneyin."));

    expect($("#submit-close").disabled).toBe(true);
    expect($("#reread-expected").textContent).toBe("Beklenen tutarı yeniden oku");
  });

  it("keeps the may-have-closed notice on screen when the state re-read fails as well", async () => {
    await openCounting({ closeNetworkDown: true, activeFailsAfterClose: true, expected: 200 });
    await type("#denom-100", "2");
    $("#submit-count").click();
    await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());

    $("#submit-close").click();
    await vi.waitFor(() => expect(text()).toContain("Kasa durumu okunamadı"));

    expect($("#cs-notice").textContent).toContain("kasa kapanmış olabilir");
  });
});
