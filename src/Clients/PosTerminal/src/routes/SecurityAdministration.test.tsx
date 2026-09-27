// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SecurityAdministration } from "./SecurityAdministration";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: body === undefined ? {} : { "Content-Type": "application/json" },
  });

/**
 * V1-RMD-331 (independent 2026-09-26 audit, finding K10): the `/settings/security`
 * screen — manager-only, per `security.manage` — is the first real client of
 * the previously-unreachable revoke-sessions/force-unlock endpoints.
 */
describe("SecurityAdministration", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("with no session, shows the manager login form", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse({ error: { code: "UNAUTHORIZED", message: "Oturum yok." } }, 401)));

    await render(<SecurityAdministration />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Yönetici girişi");
  });

  it("a session without security.manage sees the forbidden message, not the search form", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Ayşe", terminalId: "t1", capabilities: ["orders.create"] });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<SecurityAdministration />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("erişim yetkiniz yok");
    expect(document.body.textContent).not.toContain("Kullanıcı Ara");
  });

  it("a manager searches a username, sees the account, and can revoke all its sessions", async () => {
    let revokedPath: string | undefined;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["security.manage"] });
      }
      if (path.includes("/users/lookup")) {
        return jsonResponse({ userId: "target-1", displayName: "Ali Veli", active: true, isLocked: false });
      }
      if (path.includes("/revoke-sessions") && init?.method === "POST") {
        revokedPath = path;
        return jsonResponse({ userId: "target-1", revokedSessions: 2 });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<SecurityAdministration />);
    await act(async () => Promise.resolve());

    const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value")!.set!;
    const usernameField = document.querySelector<HTMLInputElement>("form input")!;
    await act(async () => {
      nativeSetter.call(usernameField, "ali.veli");
      usernameField.dispatchEvent(new Event("input", { bubbles: true }));
    });
    const searchButton = document.querySelector<HTMLButtonElement>("form button")!;
    await act(async () => searchButton.click());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Ali Veli");
    expect(document.body.textContent).not.toContain("Kilitli");

    const revokeButton = Array.from(document.querySelectorAll("button")).find((b) => b.textContent?.includes("Tüm Oturumları Sonlandır"))!;
    await act(async () => revokeButton.click());
    await act(async () => Promise.resolve());

    expect(revokedPath).toContain("target-1");
    expect(document.body.textContent).toContain("Tüm oturumlar sonlandırıldı (2 oturum)");
  });

  it("a locked account can have its lock cleared, and the unlock button disables once it is", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["security.manage"] });
      }
      if (path.includes("/users/lookup")) {
        return jsonResponse({ userId: "target-2", displayName: "Kilitli Kullanıcı", active: true, isLocked: true });
      }
      if (path.includes("/force-unlock") && init?.method === "POST") {
        return jsonResponse({ userId: "target-2", unlocked: true });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<SecurityAdministration />);
    await act(async () => Promise.resolve());

    const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value")!.set!;
    const usernameField = document.querySelector<HTMLInputElement>("form input")!;
    await act(async () => {
      nativeSetter.call(usernameField, "kilitli");
      usernameField.dispatchEvent(new Event("input", { bubbles: true }));
    });
    await act(async () => document.querySelector<HTMLButtonElement>("form button")!.click());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Kilitli");
    const unlockButton = Array.from(document.querySelectorAll("button")).find((b) => b.textContent?.includes("Hesap Kilidini Kaldır")) as HTMLButtonElement;
    expect(unlockButton.disabled).toBe(false);

    await act(async () => unlockButton.click());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Hesap kilidi kaldırıldı");
    expect(unlockButton.disabled).toBe(true);
  });

  it("an unknown username shows a Turkish not-found message, never a raw 404", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["security.manage"] });
      }
      if (path.includes("/users/lookup")) {
        return jsonResponse({ error: { code: "NOT_FOUND", message: "İstenen kullanıcı bulunamadı." } }, 404);
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<SecurityAdministration />);
    await act(async () => Promise.resolve());

    const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value")!.set!;
    const usernameField = document.querySelector<HTMLInputElement>("form input")!;
    await act(async () => {
      nativeSetter.call(usernameField, "yok-boyle-biri");
      usernameField.dispatchEvent(new Event("input", { bubbles: true }));
    });
    await act(async () => document.querySelector<HTMLButtonElement>("form button")!.click());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("bulunamadı");
    expect(document.body.textContent).not.toContain("404");
  });

  // V1-RMD-373 (module-by-module UI audit, 2026-09-27): unlike ~13 other
  // feature workspaces, none of the Ayarlar (settings) screens had an
  // axe-core scan.
  it("has no critical or serious axe violations", async () => {
    document.title = "ALKAROS güvenlik yönetimi";
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse({ error: { code: "UNAUTHORIZED", message: "Oturum yok." } }, 401)));
    await render(<SecurityAdministration />);
    await act(async () => Promise.resolve());
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
