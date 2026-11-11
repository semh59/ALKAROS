import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/cash-session");
const htmlPath = resolve(pageDir, "index.html");
const scriptPath = resolve(pageDir, "cash-session.js");

const TERMINAL = "11111111-1111-1111-1111-111111111111";
const signedIn = { test: (url) => url.endsWith("/auth/session/current"), body: { userId: "u1", displayName: "Ayşe", terminalId: TERMINAL, capabilities: [] } };
const text = () => document.body.textContent;

async function openWith(activeRoute) {
  installFetchRouter([signedIn, { test: (url) => url.endsWith("/cash-sessions/active"), ...activeRoute }]);
  await loadApp(htmlPath, scriptPath);
}

describe("cash-session.js when the active session cannot be read", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it.each([
    [500, "Sunucu tarafında bir sorun oluştu"],
    [503, "Sunucu tarafında bir sorun oluştu"],
    [429, "Çok fazla istek gönderildi"],
    [403, "Bu işlem için yetkiniz yok"],
  ])("shows a fixed Turkish sentence and a retry button for a %i, never the login form", async (status, sentence) => {
    await openWith({ status, body: { error: { message: "raw server text" } } });

    await vi.waitFor(() => expect(text()).toContain("Kasa Bilgisi Okunamadı"));
    expect(text()).toContain(sentence);
    expect(text()).not.toContain("raw server text");
    expect(document.getElementById("login-form")).toBeNull();
    expect(document.getElementById("cs-reload")).not.toBeNull();
  });

  it("leaves the checking screen when the network fails and offers a retry", async () => {
    installFetchRouter([signedIn]);
    const router = globalThis.fetch;
    vi.stubGlobal("fetch", vi.fn(async (url, init) => {
      if (String(url).endsWith("/cash-sessions/active")) throw new TypeError("Failed to fetch");
      return router(url, init);
    }));
    await loadApp(htmlPath, scriptPath);

    await vi.waitFor(() => expect(text()).toContain("Kasa Bilgisi Okunamadı"));
    expect(text()).toContain("Sunucuya ulaşılamadı");
    expect(text()).not.toContain("Oturum kontrol ediliyor");
    expect(document.getElementById("cs-reload")).not.toBeNull();
  });

  it("still sends a signed-out cashier to the login form on a 401", async () => {
    await openWith({ status: 401, body: {} });

    await vi.waitFor(() => expect(document.getElementById("login-form")).not.toBeNull());
  });

  it.each([
    [{ suggestedOpeningBalance: 1250.5 }, "Önerilen tutar: ₺1.250,50 (son kapanışta sayılan nakit). Çekmecedeki parayı sayıp gerekirse değiştirin."],
    [{ suggestedOpeningBalance: 0 }, "Önerilen tutar: ₺0,00 (son kapanışta sayılan nakit). Çekmecedeki parayı sayıp gerekirse değiştirin."],
    [{ suggestedOpeningBalance: null }, "Önceki kapanıştan öneri yok; çekmecedeki parayı sayıp yazın."],
    [{ status: 500 }, "Önceki kapanıştan öneri yok; çekmecedeki parayı sayıp yazın."],
  ])("tells the cashier what the opening amount is and what to do (%j)", async (suggestion, hint) => {
    installFetchRouter([
      signedIn,
      { test: (url) => url.endsWith("/cash-sessions/active"), status: 404, body: {} },
      { test: (url) => url.endsWith("/suggested-opening-balance"), status: suggestion.status ?? 200, body: suggestion.status ? {} : suggestion },
    ]);
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect(document.getElementById("opening-balance")).not.toBeNull());

    expect(document.getElementById("opening-hint").textContent).toBe(hint);
  });

  it("opens the cash session when Enter is pressed in the opening amount field", async () => {
    const router = installFetchRouter([
      signedIn,
      { test: (url) => url.endsWith("/cash-sessions/active"), status: 404, body: {} },
      { test: (url) => url.endsWith("/suggested-opening-balance"), body: { suggestedOpeningBalance: 100 } },
      { test: (url, init) => url.endsWith("/cash-sessions") && init?.method === "POST", status: 409, body: { error: { code: "ACTIVE_CASH_SESSION_EXISTS" } } },
    ]);
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect(document.getElementById("opening-balance")).not.toBeNull());

    const press = () => document.getElementById("opening-balance").dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", bubbles: true }));
    const posts = () => router.mock.calls.filter(([url, init]) => url.endsWith("/cash-sessions") && init?.method === "POST").length;
    press();
    press();

    await vi.waitFor(() => expect(text()).toContain("Zaten Açık Bir Kasa Var"));
    expect(posts()).toBe(1);
  });

  it.each([["Enter", "-1", true], ["a", "5", false]])("does not send an opening amount for key %s with value %s", async (key, value, showsError) => {
    const router = installFetchRouter([
      signedIn,
      { test: (url) => url.endsWith("/cash-sessions/active"), status: 404, body: {} },
      { test: (url) => url.endsWith("/suggested-opening-balance"), body: { suggestedOpeningBalance: 100 } },
    ]);
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect(document.getElementById("opening-balance")).not.toBeNull());
    const field = document.getElementById("opening-balance");
    field.value = value;

    field.dispatchEvent(new KeyboardEvent("keydown", { key, bubbles: true }));

    if (showsError) await vi.waitFor(() => expect(text()).toContain("Geçerli bir tutar girin."));
    expect(router.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
  });
});
