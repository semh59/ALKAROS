// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CustomerDisplayScreensaverSettings } from "./CustomerDisplayScreensaverSettings";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: body === undefined ? {} : { "Content-Type": "application/json" },
  });

/**
 * V1-RMD-373 (module-by-module UI audit, 2026-09-27): this file had no test
 * file at all, unlike every other Ayarlar (settings) screen
 * (RelaySettings/QnbCredentialSettings/TokenTerminalSettings/
 * SecurityAdministration/BusinessIdentitySettings/ReservationStation each
 * already had one, just missing the axe scan those got here too).
 */
describe("CustomerDisplayScreensaverSettings", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  beforeEach(() => {
    // jsdom does not implement these; the component's own preview path
    // calls them directly (BusinessIdentitySettings.test.tsx's own note).
    (URL as unknown as { createObjectURL: (blob: Blob) => string }).createObjectURL = vi.fn(() => "blob:mock-screensaver");
    (URL as unknown as { revokeObjectURL: (url: string) => void }).revokeObjectURL = vi.fn();
  });

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("with no session, shows the manager login form", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse({ error: { code: "UNAUTHORIZED", message: "Oturum yok." } }, 401)));

    await render(<CustomerDisplayScreensaverSettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Yönetici girişi");
  });

  it("a session without catalog.manage sees the forbidden message, not the upload form", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Ayşe", terminalId: "t1", capabilities: ["orders.create"] });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<CustomerDisplayScreensaverSettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("erişim yetkiniz yok");
    expect(document.querySelector('input[type="file"]')).toBeNull();
  });

  it("rejects an oversized image before it ever reaches the server", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Deniz", terminalId: "t1", capabilities: ["catalog.manage"] });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<CustomerDisplayScreensaverSettings />);
    await act(async () => Promise.resolve());

    const input = document.querySelector<HTMLInputElement>('input[type="file"]')!;
    const oversized = new File([new Uint8Array(6 * 1024 * 1024)], "big.png", { type: "image/png" });
    await act(async () => {
      Object.defineProperty(input, "files", { value: [oversized], configurable: true });
      input.dispatchEvent(new Event("change", { bubbles: true }));
    });

    expect(document.body.textContent).toContain("5 MB'ı aşamaz");
    expect(document.querySelector('button.primary')?.hasAttribute("disabled")).toBe(true);
  });

  // V1-RMD-373: unlike ~13 other feature workspaces, this screen (and every
  // other Ayarlar screen) had no axe-core scan.
  it("has no critical or serious axe violations", async () => {
    document.title = "ALKAROS ekran koruyucu ayarları";
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse({ error: { code: "UNAUTHORIZED", message: "Oturum yok." } }, 401)));
    await render(<CustomerDisplayScreensaverSettings />);
    await act(async () => Promise.resolve());
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
