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
const failure = (status, code) => reply(status, { error: { code, message: "RAW SERVER TEXT" } });
const variance = () => failure(409, "CASH_VARIANCE_THRESHOLD_EXCEEDED");
const $ = (selector) => document.querySelector(selector);
const text = () => document.body.textContent;

function install(closeReplies) {
  const calls = [];
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    calls.push({ url, method, body: init?.body ? JSON.parse(init.body) : null });
    if (url.endsWith("/auth/session/current")) return reply(200, { userId: "u1", displayName: "Ayşe", terminalId: TERMINAL, capabilities: [] });
    if (url.endsWith("/cash-sessions/active")) return reply(200, session);
    if (url.endsWith("/expected-cash")) return reply(200, { expectedCash: 1000, varianceTolerance: 50, openBillCount: 0, unsettledPaymentCount: 0 });
    if (url.endsWith("/cash-movements") && method === "GET") return reply(200, emptyList);
    if (url.endsWith("/start-count")) return reply(200, { ...session, status: "Counting" });
    if (url.endsWith("/counts")) return reply(200, {});
    if (url.endsWith("/close")) {
      return closeReplies.length
        ? closeReplies.shift()
        : reply(200, { session: { ...session, closedAt: "2026-11-03T20:00:00Z" }, difference: -500 });
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

const closes = (calls) => calls.filter((c) => c.method === "POST" && c.url.endsWith("/close"));

/** Counts 500 TL against an expected 1000, refuses the first close for the tolerance and lands on the manager block. */
async function toManagerBlock(closeReplies = []) {
  const calls = install([variance(), ...closeReplies]);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect($("#start-count")).not.toBeNull());
  $("#start-count").click();
  $("#start-count-confirm").click();
  await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());
  await type("#denom-100", "5");
  $("#submit-count").click();
  await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());
  await type("#difference-explanation", "sayım doğru");
  $("#submit-close").click();
  await vi.waitFor(() => expect($("#manager-username")).not.toBeNull());
  return calls;
}

async function approve(username, password) {
  await type("#manager-username", username);
  await type("#manager-password", password);
  $("#submit-close").click();
}

describe("cash-session.js manager approval at the till", () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, "", "/");
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("offers no manager fields while the tolerance holds", async () => {
    const calls = install([]);
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#start-count")).not.toBeNull());
    $("#start-count").click();
    $("#start-count-confirm").click();
    await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());
    await type("#denom-100", "10");
    $("#submit-count").click();
    await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());

    expect($("#manager-username")).toBeNull();
    expect($("#manager-password")).toBeNull();
    expect(closes(calls)).toHaveLength(0);
  });

  it("tells the cashier that the manager types the name and password", async () => {
    await toManagerBlock();

    expect($('[role="alert"]').textContent).toContain("yönetici yanınızda kendi kullanıcı adı ve şifresini girsin");
  });

  it("offers labelled manager name and password fields once the tolerance is exceeded", async () => {
    await toManagerBlock();

    expect(document.querySelector('label[for="manager-username"]').textContent).toBe("Yönetici kullanıcı adı");
    expect(document.querySelector('label[for="manager-password"]').textContent).toBe("Yönetici şifresi");
    expect($("#manager-password").type).toBe("password");
    expect($("#manager-password").getAttribute("autocomplete")).toBe("new-password");
    expect($("#manager-username").getAttribute("autocomplete")).toBe("off");
  });

  it("sends the manager name and password with the override and shows the closed summary", async () => {
    const calls = await toManagerBlock();

    await approve("mudur", "Mudur-Sifre-1");

    await vi.waitFor(() => expect(text()).toContain("Kasa Kapatıldı"));
    expect(closes(calls)[1].body).toMatchObject({
      IsSupervisorOverride: true, OverrideReason: "sayım doğru", ManagerUsername: "mudur", ManagerPassword: "Mudur-Sifre-1",
    });
  });

  it("stops with a Turkish sentence and sends nothing when only one of the two fields is filled", async () => {
    const calls = await toManagerBlock();

    await type("#manager-username", "mudur");
    $("#submit-close").click();
    await vi.waitFor(() => expect(text()).toContain("Yönetici kullanıcı adı ve şifresi birlikte girilmelidir."));
    await type("#manager-username", "");
    await type("#manager-password", "x");
    $("#submit-close").click();

    await vi.waitFor(() => expect(text()).toContain("Yönetici kullanıcı adı ve şifresi birlikte girilmelidir."));
    expect(closes(calls)).toHaveLength(1);
  });

  it("sends no credentials when both fields are empty, so a manager closing under their own account still works", async () => {
    const calls = await toManagerBlock();

    $("#submit-close").click();

    await vi.waitFor(() => expect(closes(calls)).toHaveLength(2));
    expect(closes(calls)[1].body).not.toHaveProperty("ManagerUsername");
    expect(closes(calls)[1].body).not.toHaveProperty("ManagerPassword");
    expect(closes(calls)[1].body.IsSupervisorOverride).toBe(true);
  });

  it.each([
    ["MANAGER_CREDENTIAL_INVALID", 403, "Yönetici adı veya şifre hatalı."],
    ["MANAGER_LOCKED", 403, "Bu yönetici hesabı geçici olarak kilitlendi; en geç 15 dakika sonra açılır. Yönetici kendi ekranından onaylayabilir."],
    ["TOO_MANY_ATTEMPTS", 429, "Çok fazla hatalı deneme yapıldı. En geç 15 dakika sonra tekrar deneyin ya da yönetici kendi ekranından onaylasın."],
    ["SELF_APPROVAL_NOT_ALLOWED", 403, "Kendi talebinizi onaylayamazsınız; yöneticinin kendi hesabıyla girmesi gerekir."],
    ["APPROVER_LACKS_PERMISSION", 403, "Bu yönetici bu işlemi onaylama yetkisine sahip değil."],
  ])("says %s with one fixed sentence, never the server text, and empties the password", async (code, status, sentence) => {
    await toManagerBlock([failure(status, code)]);

    await approve("mudur", "Mudur-Sifre-1");

    await vi.waitFor(() => expect(text()).toContain(sentence));
    expect(text()).not.toContain("RAW SERVER TEXT");
    expect(text()).not.toContain(code);
    expect($("#manager-password").value).toBe("");
    expect($("#manager-username").value).toBe("mudur");
    expect($("#manager-username")).not.toBeNull();
  });

  it.each([
    "Yönetici adı veya şifre hatalı.",
    "Bu yönetici hesabı geçici olarak kilitlendi; en geç 15 dakika sonra açılır. Yönetici kendi ekranından onaylayabilir.",
    "Çok fazla hatalı deneme yapıldı. En geç 15 dakika sonra tekrar deneyin ya da yönetici kendi ekranından onaylasın.",
    "Kendi talebinizi onaylayamazsınız; yöneticinin kendi hesabıyla girmesi gerekir.",
    "Bu yönetici bu işlemi onaylama yetkisine sahip değil.",
  ])("uses the very sentence the server sends: %s", (sentence) => {
    const catalog = readFileSync(resolve(here, "..", "..", "..", "src/Host/Composition/Errors/ApiErrorCatalog.cs"), "utf8");
    expect(catalog).toContain(`"${sentence}"`);
  });

  it("keeps the password out of every place the page could keep it", async () => {
    const storage = vi.spyOn(Storage.prototype, "setItem");
    const calls = await toManagerBlock([failure(403, "MANAGER_CREDENTIAL_INVALID")]);

    await approve("mudur", "Gizli-Sifre-99");
    await vi.waitFor(() => expect(text()).toContain("Yönetici adı veya şifre hatalı."));

    expect(JSON.stringify(storage.mock.calls)).not.toContain("Gizli-Sifre-99");
    expect(window.location.href).not.toContain("Gizli-Sifre-99");
    expect(document.body.innerHTML).not.toContain("Gizli-Sifre-99");
    expect(calls.filter((c) => !c.url.endsWith("/close")).map((c) => JSON.stringify(c.body)).join()).not.toContain("Gizli-Sifre-99");
  });
});
