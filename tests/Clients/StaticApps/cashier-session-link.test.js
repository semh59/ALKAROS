import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..", "..");
const htmlPath = resolve(repoRoot, "src/Clients/Cashier/wwwroot/index.html");
const scriptPath = resolve(repoRoot, "src/Clients/Cashier/wwwroot/cashier-app.js");

describe("cashier-app.js without a session", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("tells the cashier to sign in and offers the login page", async () => {
    installFetchRouter([{ test: (url) => url.endsWith("/api/v1/auth/session/current"), status: 401, body: {} }]);
    await loadApp(htmlPath, scriptPath);

    await vi.waitFor(() => expect(document.body.textContent).toContain("Kasiyer oturumu doğrulanamadı"));
    const link = [...document.querySelectorAll("a")].find((a) => a.textContent === "Giriş sayfasına git");
    expect(link).toBeTruthy();
    expect(link.getAttribute("href")).toBe("/");
  });
});
