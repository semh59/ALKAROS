// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { QnbCredentialSettings } from "./QnbCredentialSettings";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: body === undefined ? {} : { "Content-Type": "application/json" },
  });

const emptyStatus = { configured: false, updatedAt: null, userId: null, vergiTcKimlikNo: null };

/**
 * V14-QNB-006: the `/settings/qnb-credential` screen — manager-only, per
 * `integrations.manage`, and the saved password must never come back
 * from either the save call's own response or a later status read.
 */
describe("QnbCredentialSettings", () => {
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

  const setValue = async (input: HTMLInputElement, value: string) => {
    const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value")!.set!;
    await act(async () => {
      nativeSetter.call(input, value);
      input.dispatchEvent(new Event("input", { bubbles: true }));
    });
  };

  it("with no session, shows the manager login form", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse({ error: { code: "UNAUTHORIZED", message: "Oturum yok." } }, 401)));

    await render(<QnbCredentialSettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Yönetici girişi");
  });

  it("a session without integrations.manage sees the forbidden message, not the settings form", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Ayşe", terminalId: "t1", capabilities: ["orders.create"] });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<QnbCredentialSettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("erişim yetkiniz yok");
    expect(document.body.textContent).not.toContain("Vergi kimlik");
  });

  it("every visible label is Turkish", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["integrations.manage"] });
      }
      if (path.endsWith("/qnb-credential/status")) {
        return jsonResponse(emptyStatus);
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<QnbCredentialSettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("QNB kullanıcı adı");
    expect(document.body.textContent).toContain("QNB parolası");
    expect(document.body.textContent).toContain("Vergi kimlik numarası");
  });

  it("a manager can save a credential and status reflects it, with the password never echoed back", async () => {
    let savedBody: string | undefined;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["integrations.manage"] });
      }
      if (path.endsWith("/qnb-credential/status")) {
        return jsonResponse(emptyStatus);
      }
      if (path.endsWith("/qnb-credential/") && init?.method === "POST") {
        savedBody = String(init.body);
        return jsonResponse(undefined, 204);
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<QnbCredentialSettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Henüz yapılandırılmadı");

    const [userIdField, passwordField, vknField] = document.querySelectorAll<HTMLInputElement>("form input");
    await setValue(userIdField, "UserID");
    await setValue(passwordField, "cs-super-secret-password");
    await setValue(vknField, "3250566851");

    const submitButton = document.querySelector<HTMLButtonElement>('button[type="submit"], form button')!;
    await act(async () => submitButton.click());
    await act(async () => Promise.resolve());

    expect(savedBody).toContain("UserID");
    expect(savedBody).toContain("cs-super-secret-password");
    expect(savedBody).toContain("3250566851");
    expect(document.body.textContent).not.toContain("cs-super-secret-password");
    expect(document.body.textContent).toContain("kaydedildi");
  });

  it("shows the persisted (non-secret) fields when already configured", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["integrations.manage"] });
      }
      if (path.endsWith("/qnb-credential/status")) {
        return jsonResponse({ configured: true, updatedAt: "2026-09-18T10:00:00Z", userId: "UserID", vergiTcKimlikNo: "3250566851" });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<QnbCredentialSettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Yapılandırıldı");
    const [userIdField, , vknField] = document.querySelectorAll<HTMLInputElement>("form input");
    expect(userIdField.value).toBe("UserID");
    expect(vknField.value).toBe("3250566851");
  });

  it("shows the 'Test Connection' button only once configured, and shows the sanitized result", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["integrations.manage"] });
      }
      if (path.endsWith("/qnb-credential/status")) {
        return jsonResponse({ configured: true, updatedAt: "2026-09-18T10:00:00Z", userId: "UserID", vergiTcKimlikNo: "3250566851" });
      }
      if (path.endsWith("/qnb-credential/test-connection") && init?.method === "POST") {
        return jsonResponse({ success: false, message: "QNB'ye bağlanılamadı; kullanıcı adı veya parola hatalı olabilir." });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<QnbCredentialSettings />);
    await act(async () => Promise.resolve());

    const testButton = Array.from(document.querySelectorAll("button")).find((b) => b.textContent?.includes("Bağlantıyı Test Et"));
    expect(testButton).toBeTruthy();

    await act(async () => testButton!.click());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("kullanıcı adı veya parola hatalı olabilir");
    // Never the raw QNB SOAP fault code/text, only the pre-written Turkish sentence.
    expect(document.body.textContent).not.toContain("EF0003");
  });

  it("shows the business details card for a manager and keeps it away from a session without integrations.manage", async () => {
    const stub = (capabilities: string[]) => vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities });
      }
      if (path.endsWith("/qnb-credential/status")) return jsonResponse(emptyStatus);
      if (path.endsWith("/invoice-settings/seller-profile")) return jsonResponse({ configured: false, profile: null });
      throw new Error(`unexpected fetch: ${path}`);
    }));

    stub(["integrations.manage"]);
    await render(<QnbCredentialSettings />);
    await act(async () => Promise.resolve());
    expect(document.body.textContent).toContain("İşletme bilgileri");
    expect(document.body.textContent).toContain("Ticari ünvan");

    await act(async () => root!.unmount());
    root = null;
    stub(["orders.create"]);
    await render(<QnbCredentialSettings />);
    await act(async () => Promise.resolve());
    expect(document.body.textContent).not.toContain("Ticari ünvan");
  });

  it("does not show the 'Test Connection' button before a credential is saved", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["integrations.manage"] });
      }
      if (path.endsWith("/qnb-credential/status")) {
        return jsonResponse(emptyStatus);
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<QnbCredentialSettings />);
    await act(async () => Promise.resolve());

    const testButton = Array.from(document.querySelectorAll("button")).find((b) => b.textContent?.includes("Bağlantıyı Test Et"));
    expect(testButton).toBeUndefined();
  });

  // V1-RMD-373 (module-by-module UI audit, 2026-09-27): unlike ~13 other
  // feature workspaces, none of the Ayarlar (settings) screens had an
  // axe-core scan.
  it("has no critical or serious axe violations", async () => {
    document.title = "ALKAROS QNB ayarları";
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse({ error: { code: "UNAUTHORIZED", message: "Oturum yok." } }, 401)));
    await render(<QnbCredentialSettings />);
    await act(async () => Promise.resolve());
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
