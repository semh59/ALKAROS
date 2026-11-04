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
});
