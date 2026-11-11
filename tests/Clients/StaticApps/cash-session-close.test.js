import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { readFileSync } from "node:fs";
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
const text = () => document.body.textContent;

/** A scripted server for the closing screen: the expected-cash answer and the close answer come from `world`. */
function install(world) {
  const calls = [];
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    calls.push({ url, method, body: init?.body ? JSON.parse(init.body) : null });
    if (url.endsWith("/auth/session/current")) return reply(200, { userId: "u1", displayName: "Ayşe", terminalId: TERMINAL, capabilities: [] });
    if (url.endsWith("/cash-sessions/active")) return reply(200, session);
    if (url.endsWith("/expected-cash")) {
      return reply(200, { expectedCash: 1000, varianceTolerance: 50, openBillCount: 0, unsettledPaymentCount: 0, ...world.expected });
    }
    if (url.endsWith("/cash-movements") && method === "GET") return reply(200, emptyList);
    if (url.endsWith("/start-count")) return reply(200, { ...session, status: "Counting" });
    if (url.endsWith("/counts")) return reply(200, {});
    if (url.endsWith("/close")) {
      if (world.closeReplies?.length) return world.closeReplies.shift();
      return reply(200, { session: { ...session, closedAt: "2026-11-03T20:00:00Z" }, difference: 0 });
    }
    throw new Error(`unrouted ${method} ${url}`);
  }));
  return calls;
}

async function type(selector, value) {
  const element = $(selector);
  element.value = value;
  element.dispatchEvent(new Event("input", { bubbles: true }));
}

/** Opens the drawer, counts `hundreds` x 100 and lands on the closing screen. */
async function toClosing(world = {}, hundreds = 10) {
  const calls = install(world);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect($("#start-count")).not.toBeNull());
  $("#start-count").click();
  await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());
  await type("#denom-100", String(hundreds));
  $("#submit-count").click();
  await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());
  return calls;
}

const closes = (calls) => calls.filter((c) => c.method === "POST" && c.url.endsWith("/close"));

describe("cash-session.js closing screen", () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, "", "/");
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("warns about open bills and unsettled payments and keeps closing off until they are knowingly accepted", async () => {
    const calls = await toClosing({ expected: { openBillCount: 2, unsettledPaymentCount: 1 } });

    expect($("#unsettled-warning").textContent).toContain("2 açık hesap veya sipariş, 1 sonucu belirsiz ödeme");
    expect($("#submit-close").disabled).toBe(true);
    $("#submit-close").click();
    expect(closes(calls)).toHaveLength(0);

    $("#ack-unsettled").click();
    await vi.waitFor(() => expect($("#submit-close").disabled).toBe(false));
    $("#submit-close").click();
    await vi.waitFor(() => expect(closes(calls)).toHaveLength(1));
    expect(closes(calls)[0].body.AcknowledgeUnsettled).toBe(true);
  });

  it.each([
    ["only open bills", { openBillCount: 3 }, "3 açık hesap veya sipariş, 0 sonucu belirsiz ödeme"],
    ["only unsettled payments", { unsettledPaymentCount: 2 }, "0 açık hesap veya sipariş, 2 sonucu belirsiz ödeme"],
  ])("warns for %s", async (_name, expected, sentence) => {
    await toClosing({ expected });

    expect($("#unsettled-warning").textContent).toContain(sentence);
    expect($("#submit-close").disabled).toBe(true);
  });

  it("shows no warning and no checkbox when nothing is open, and closes with the acknowledgement off", async () => {
    const calls = await toClosing();

    expect($("#unsettled-warning")).toBeNull();
    expect($("#ack-unsettled")).toBeNull();
    $("#submit-close").click();
    await vi.waitFor(() => expect(closes(calls)).toHaveLength(1));
    expect(closes(calls)[0].body.AcknowledgeUnsettled).toBe(false);
    expect(closes(calls)[0].body.DifferenceExplanation).toBeNull();
  });

  it("asks for an explanation of any difference before sending and sends it as the difference explanation", async () => {
    const calls = await toClosing({}, 9);

    expect($("#difference-explanation")).not.toBeNull();
    $("#submit-close").click();
    await vi.waitFor(() => expect(text()).toContain("fark açıklaması yazın"));
    expect(closes(calls)).toHaveLength(0);

    await type("#difference-explanation", "  para üstü eksik verildi ");
    $("#submit-close").click();
    await vi.waitFor(() => expect(closes(calls)).toHaveLength(1));
    expect(closes(calls)[0].body).toMatchObject({
      ActualCash: 900, IsSupervisorOverride: false, OverrideReason: null, DifferenceExplanation: "para üstü eksik verildi",
    });
  });

  it("asks for no explanation when the count matches", async () => {
    await toClosing();

    expect($("#difference-explanation")).toBeNull();
    expect($("#override-reason")).toBeNull();
  });

  it("shows the manager block once, without an error box saying the same, and sends one text as both reasons", async () => {
    const variance = reply(409, { error: { code: "CASH_VARIANCE_THRESHOLD_EXCEEDED", message: "RAW" } });
    const calls = await toClosing({ closeReplies: [variance] }, 5);
    await type("#difference-explanation", "sayım doğru");

    $("#submit-close").click();
    await vi.waitFor(() => expect($("#override-reason")).not.toBeNull());

    expect(document.body.innerHTML.split("Fark tolerans sınırını aşıyor").length - 1).toBe(1);
    expect(document.querySelectorAll('[role="alert"]')).toHaveLength(1);
    expect($('[role="alert"]').textContent).toContain("Fark tolerans sınırını aşıyor");
    expect(text()).toContain("Yönetici Açıklaması");
    expect(text()).not.toContain("üpervizör");
    expect($("#override-reason").value).toBe("sayım doğru");
    $("#submit-close").click();
    await vi.waitFor(() => expect(closes(calls)).toHaveLength(2));
    expect(closes(calls)[1].body).toMatchObject({ IsSupervisorOverride: true, OverrideReason: "sayım doğru", DifferenceExplanation: "sayım doğru" });
  });

  it("sends the acknowledgement together with the manager override, kept across the first refusal", async () => {
    const variance = reply(409, { error: { code: "CASH_VARIANCE_THRESHOLD_EXCEEDED", message: "RAW" } });
    const calls = await toClosing({ expected: { openBillCount: 1 }, closeReplies: [variance] }, 5);
    $("#ack-unsettled").click();
    await vi.waitFor(() => expect($("#submit-close").disabled).toBe(false));
    await type("#difference-explanation", "sayım doğru");
    $("#submit-close").click();
    await vi.waitFor(() => expect($("#override-reason")).not.toBeNull());

    expect($("#ack-unsettled").checked).toBe(true);
    $("#submit-close").click();
    await vi.waitFor(() => expect(closes(calls)).toHaveLength(2));
    expect(closes(calls)[1].body).toMatchObject({ AcknowledgeUnsettled: true, IsSupervisorOverride: true });
  });

  it("clears an earlier failure when the next answer is the manager block, so only the block warns", async () => {
    const failure = reply(500, { error: { code: "INTERNAL", message: "RAW" } });
    const variance = reply(409, { error: { code: "CASH_VARIANCE_THRESHOLD_EXCEEDED", message: "RAW" } });
    const calls = await toClosing({ closeReplies: [failure, variance] }, 5);
    await type("#difference-explanation", "sayım doğru");

    $("#submit-close").click();
    await vi.waitFor(() => expect($('[role="alert"]')).not.toBeNull());
    $("#submit-close").click();
    await vi.waitFor(() => expect($("#override-reason")).not.toBeNull());

    expect(closes(calls)).toHaveLength(2);
    expect(document.querySelectorAll('[role="alert"]')).toHaveLength(1);
    expect($('[role="alert"]').textContent).not.toContain("RAW");
  });

  it("will not send the manager override with an empty explanation", async () => {
    const variance = reply(409, { error: { code: "CASH_VARIANCE_THRESHOLD_EXCEEDED", message: "RAW" } });
    const calls = await toClosing({ closeReplies: [variance] }, 5);
    await type("#difference-explanation", "sayım doğru");
    $("#submit-close").click();
    await vi.waitFor(() => expect($("#override-reason")).not.toBeNull());

    await type("#override-reason", "   ");
    $("#submit-close").click();

    await vi.waitFor(() => expect(text()).toContain("Fark tolerans sınırını aşıyor; bir açıklama girin."));
    expect(closes(calls)).toHaveLength(1);
  });

  it("writes the tolerance the server reports and copies no number of its own", async () => {
    await toClosing({ expected: { varianceTolerance: 120 } });

    expect($("#close-tolerance").textContent).toMatch(/120/);
    expect($("#close-tolerance").textContent).not.toMatch(/\b50\b/);
  });

  it("keeps the typed explanation and the acknowledgement through the re-render of ticking the box", async () => {
    await toClosing({ expected: { openBillCount: 1 } }, 9);
    await type("#difference-explanation", "yazılan");

    $("#ack-unsettled").click();
    await vi.waitFor(() => expect($("#submit-close").disabled).toBe(false));

    expect($("#difference-explanation").value).toBe("yazılan");
    expect($("#ack-unsettled").checked).toBe(true);
  });

  it.each([
    ["CASH_SESSION_OWNER_REQUIRED", 403, "yalnız oturumu açan kasiyer veya bir yönetici"],
    ["CASH_COUNT_REQUIRED", 409, "Kasa sayımı kaydedilmeden"],
    ["CASH_COUNT_MISMATCH", 409, "son sayımla aynı olmalı"],
    ["CASH_DIFFERENCE_EXPLANATION_REQUIRED", 400, "1 ile 500 karakter"],
    ["CASH_CLOSE_UNSETTLED_NOT_ACKNOWLEDGED", 409, "onay kutusunu işaretleyin"],
    ["CASH_RECONCILE_SELF_NOT_ALLOWED", 403, "başka bir yönetici onaylamalı"],
  ])("shows %s as its Turkish text and never the raw server message", async (code, status, expectedText) => {
    const calls = await toClosing({ closeReplies: [reply(status, { error: { code, message: "RAW SERVER TEXT" } })] });

    $("#submit-close").click();
    await vi.waitFor(() => expect(closes(calls)).toHaveLength(1));
    await vi.waitFor(() => expect(text()).toContain(expectedText));

    expect(text()).not.toContain("RAW SERVER TEXT");
  });

  it("keeps keyboard focus on the checkbox after ticking it", async () => {
    await toClosing({ expected: { openBillCount: 1 } });

    $("#ack-unsettled").click();
    await vi.waitFor(() => expect($("#submit-close").disabled).toBe(false));

    expect(document.activeElement).toBe($("#ack-unsettled"));
  });

  it("reads the counts again when the server says something opened after they were read, so the box appears", async () => {
    const refusal = reply(409, { error: { code: "CASH_CLOSE_UNSETTLED_NOT_ACKNOWLEDGED", message: "RAW" } });
    const world = { closeReplies: [refusal] };
    const calls = await toClosing(world);
    expect($("#ack-unsettled")).toBeNull();

    world.expected = { openBillCount: 1 };
    $("#submit-close").click();

    await vi.waitFor(() => expect($("#ack-unsettled")).not.toBeNull());
    expect($("#submit-close").disabled).toBe(true);
    expect(closes(calls)).toHaveLength(1);
  });

  it("starts the next shift without the acknowledgement or explanation of the last one", async () => {
    const calls = await toClosing({ expected: { openBillCount: 1 } }, 9);
    await type("#difference-explanation", "eski");
    $("#ack-unsettled").click();
    await vi.waitFor(() => expect($("#submit-close").disabled).toBe(false));
    $("#submit-close").click();
    await vi.waitFor(() => expect($("#reopen")).not.toBeNull());
    expect(closes(calls)).toHaveLength(1);

    $("#reopen").click();
    await vi.waitFor(() => expect($("#start-count")).not.toBeNull());
    $("#start-count").click();
    await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());
    await type("#denom-100", "9");
    $("#submit-count").click();
    await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());

    expect($("#difference-explanation").value).toBe("");
    expect($("#ack-unsettled").checked).toBe(false);
    expect($("#submit-close").disabled).toBe(true);
  });
});

describe("cash-session page markup", () => {
  it("does not mark the whole page as one live region", () => {
    const html = readFileSync(htmlPath, "utf-8");

    expect(html).not.toContain("aria-live");
  });

  it("draws the failed-to-load screen with classes the stylesheet defines", () => {
    const css = readFileSync(resolve(pageDir, "cash-session.css"), "utf-8");
    const script = readFileSync(scriptPath, "utf-8");
    const loadError = script.slice(script.indexOf("function renderLoadError"), script.indexOf("function screenKey"));
    const classes = [...loadError.matchAll(/class="([^"]+)"/g)].flatMap((m) => m[1].split(/\s+/));

    expect(classes.length).toBeGreaterThan(0);
    for (const name of classes) expect(css).toContain(`.${name}`);
  });

  it("defines the styles of the new warning and checkbox", () => {
    const css = readFileSync(resolve(pageDir, "cash-session.css"), "utf-8");

    expect(css).toContain(".cs-alert-warning");
    expect(css).toContain(".cs-check");
  });
});
