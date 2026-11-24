import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";

const reply = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body, headers: { get: () => null } });
const $ = (selector) => document.querySelector(selector);
const chip = (method) => $(`.sp-method-chip[data-method="${method}"]`);

/** A scripted server; `world.terminal` is whether a card terminal is defined, `world.answer` replaces the external-card reply. */
function install(world) {
  const posts = [];
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    if (url.endsWith("/api/v1/auth/session/current")) return reply(200, { terminalId: TERMINAL_ID, userId: "u1" });
    if (url.endsWith("/tenders/") && method === "GET") {
      return reply(200, { billId: BILL_ID, payableAmount: 100, allocatedTotal: world.paid ? 100 : 0, remainingAmount: world.paid ? 0 : 100, allocations: world.paid ? [{ allocationId: "a1", paymentId: "p1", amount: 100, paymentStatus: "Approved", method: "ExternalCard" }] : [], unsettledPayment: null, cardTerminalConfigured: world.terminal });
    }
    if (url.includes("/cash-sessions/active")) return reply(200, { cashSessionId: "66666666-6666-6666-6666-666666666666", cashierUserId: "u1" });
    if (url.endsWith("/adjustments")) return reply(200, { adjustments: [], summary: null });
    if (url.endsWith("/refund-requests")) return reply(200, world.refunds ?? []);
    if (url.endsWith("/show-on-display")) return reply(204, {});
    if (url.endsWith("/tenders/external-card") && method === "POST") {
      posts.push(JSON.parse(init.body));
      if (world.networkDown) throw new TypeError("Failed to fetch");
      return world.answer ?? reply(200, { outcome: "Approved", approvedAmount: 100, closed: false });
    }
    throw new Error(`unrouted ${method} ${url}`);
  }));
  return posts;
}

async function visit() {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect($("#submit-tender")).not.toBeNull());
}

async function typeSlip(value) {
  const input = $("#external-slip");
  input.value = value;
  input.dispatchEvent(new Event("input", { bubbles: true }));
}

async function chooseExternalCard() {
  chip("ExternalCard").click();
  await vi.waitFor(() => expect($("#external-slip")).not.toBeNull());
}

describe("split-payment.js bank POS card method", () => {
  let world;
  beforeEach(() => {
    vi.resetModules();
    window.sessionStorage.clear();
    world = { terminal: false };
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("offers the method only while no card terminal is defined, in place of the connected card", async () => {
    install(world);
    await visit();
    expect(chip("ExternalCard").textContent).toBe("Harici POS kartı");
    expect(chip("BankCard")).toBeNull();

    document.body.innerHTML = "";
    vi.resetModules();
    world.terminal = true;
    await visit();
    expect(chip("ExternalCard")).toBeNull();
    expect(chip("BankCard")).not.toBeNull();
  });

  it("shows a 32-character slip field with the card-number warning, and the button stays off until the slip is valid", async () => {
    install(world);
    await visit();
    await chooseExternalCard();

    expect($("#external-slip").getAttribute("maxlength")).toBe("32");
    expect($("#external-slip-warning").textContent).toBe("Kart numarasını değil, yalnız POS fişindeki fiş numarasını yazın.");
    expect($("#submit-tender").disabled).toBe(true);
    expect($("#submit-hint").textContent).toBe("Devam etmek için POS fiş numarasını yazın.");
    for (const slip of ["   ", "AB1", "AB 12", "AB#12", "-AB12"]) {
      await typeSlip(slip);
      expect($("#submit-tender").disabled, `"${slip}" is not a valid slip`).toBe(true);
    }
    await typeSlip("4111 1111 1111 1111");
    expect($("#submit-tender").disabled).toBe(true);
    expect($("#external-slip").getAttribute("autocomplete")).toBe("off");
    await typeSlip("4111-1111-1111-1111");
    expect($("#submit-tender").disabled, "separators do not hide a card number").toBe(true);
    await typeSlip("4111111111111111");
    expect($("#submit-tender").disabled).toBe(true);
    expect($("#submit-hint").textContent).toContain("kart numarasına benziyor");

    await typeSlip("1234567890123456");
    expect($("#submit-tender").disabled, "a long number that is not a card number is a slip").toBe(false);
    await typeSlip("POS-4471");
    expect($("#submit-tender").disabled).toBe(false);
    expect($("#submit-hint").hidden).toBe(true);
  });

  it("sends the trimmed slip to the external-card endpoint and clears the field after success", async () => {
    const posts = install(world);
    await visit();
    await chooseExternalCard();
    await typeSlip("  pos-4471 ");
    $("#submit-tender").click();
    await vi.waitFor(() => expect(posts).toHaveLength(1));

    expect(posts[0]).toMatchObject({ Amount: 100, SlipNumber: "pos-4471" });
    expect(posts[0].IdempotencyKey).toMatch(/\S/);
    await vi.waitFor(() => expect($("#external-slip")?.value ?? "").toBe(""));
  });

  it("replays the same key for a repeated attempt of the same slip and a new key for another slip", async () => {
    world.networkDown = true;
    const posts = install(world);
    await visit();
    await chooseExternalCard();
    await typeSlip("POS-4471");
    $("#submit-tender").click();
    await vi.waitFor(() => expect(posts).toHaveLength(1));
    await vi.waitFor(() => expect($("#submit-tender").disabled).toBe(false));
    $("#submit-tender").click();
    await vi.waitFor(() => expect(posts).toHaveLength(2));
    await vi.waitFor(() => expect($("#submit-tender").disabled).toBe(false));
    await typeSlip("POS-4472");
    $("#submit-tender").click();
    await vi.waitFor(() => expect(posts).toHaveLength(3));

    expect(posts[1].IdempotencyKey).toBe(posts[0].IdempotencyKey);
    expect(posts[2].IdempotencyKey).not.toBe(posts[0].IdempotencyKey);
  });

  it.each([
    ["EXTERNAL_CARD_SLIP_REUSED", 409, "Bu fiş numarası bu hesapta zaten kayıtlı. Aynı fişi iki kez girmeyin."],
    ["EXTERNAL_CARD_SLIP_INVALID", 400, "Fiş numarası 4-32 karakter olmalı (harf, rakam, - ve /). Kart numarasını yazmayın."],
    ["EXTERNAL_CARD_SLIP_IS_CARD_NUMBER", 400, "Bu bir kart numarasına benziyor. Kart numarasını yazmayın; yalnız POS fişindeki fiş numarasını yazın."],
  ])("answers %s with its Turkish sentence and never the raw code", async (code, status, sentence) => {
    world.answer = reply(status, { error: { code, message: "raw server text" } });
    const posts = install(world);
    await visit();
    await chooseExternalCard();
    await typeSlip("POS-4471");
    $("#submit-tender").click();
    await vi.waitFor(() => expect(posts).toHaveLength(1));

    await vi.waitFor(() => expect(document.body.textContent).toContain(sentence));
    expect(document.body.textContent).not.toContain(code);
    expect(document.body.textContent).not.toContain("raw server text");
  });

  it("asks for the refund slip of a bank POS card payment and says the refund is made on that POS", async () => {
    world.paid = true;
    world.refunds = [{
      requestId: "r1", billId: BILL_ID, paymentId: "p1", amount: 100, method: "ExternalCard", reason: "Yanlış tutar", status: "AwaitingManualRefund",
      requestedBy: "u1", requestedAt: "2026-10-13T10:00:00Z", decidedBy: null, decidedAt: null, slipNumber: null, billCancelled: false,
    }];
    install(world);
    window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
    await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));

    await vi.waitFor(() => expect(document.querySelector('[data-refund-slip="r1"]')).not.toBeNull());
    expect(document.body.textContent).toContain("Kart iadesi banka POS cihazında yapılır; çıkan iade fişinin numarasını yazın.");
    expect(document.body.textContent).toContain("Harici POS kartı");
    expect(document.body.textContent).not.toContain("ExternalCard");
  });

  it("drops the selection and shows the connected card when the server says a terminal is now defined", async () => {
    world.answer = reply(409, { error: { code: "CARD_TERMINAL_CONFIGURED", message: "x" } });
    const posts = install(world);
    await visit();
    await chooseExternalCard();
    await typeSlip("POS-4471");
    world.terminal = true;
    $("#submit-tender").click();
    await vi.waitFor(() => expect(posts).toHaveLength(1));

    await vi.waitFor(() => expect(chip("ExternalCard")).toBeNull());
    expect(chip("BankCard")).not.toBeNull();
    expect($("#external-slip")).toBeNull();
    expect(document.body.textContent).toContain("Kart terminali tanımlı; harici POS kartı kullanılamaz.");
  });
});
