// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { TokenTerminalSettings } from "./TokenTerminalSettings";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: body === undefined ? {} : { "Content-Type": "application/json" },
  });

const emptyStatus = {
  configured: false, updatedAt: null, merchantId: null, branchId: null, terminalId: null, clientId: null,
};

/**
 * V13-HUG-005: the `/settings/token-terminal` screen — manager-only, per
 * `integrations.manage`, and the saved client secret must never come back
 * from either the save call's own response or a later status read.
 */
describe("TokenTerminalSettings", () => {
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

    await render(<TokenTerminalSettings />);
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

    await render(<TokenTerminalSettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("erişim yetkiniz yok");
    expect(document.body.textContent).not.toContain("Client Secret");
  });

  it("every visible label is Turkish, never a bare English field name", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["integrations.manage"] });
      }
      if (path.endsWith("/token-credential/status")) {
        return jsonResponse(emptyStatus);
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<TokenTerminalSettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Müşteri kimliği");
    expect(document.body.textContent).toContain("Gizli anahtar");
    // The English term is allowed only as a parenthetical clarification
    // alongside its Turkish label (docs/UI_STYLE_GUIDE.md §1) — never bare.
    expect(document.body.textContent).not.toMatch(/(?<!\()Client ID(?!\))/);
    expect(document.body.textContent).not.toMatch(/(?<!\()Client Secret(?!\))/);
  });

  it("a manager sees the current status and can save a new credential, which is never echoed back", async () => {
    let savedBody: string | undefined;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["integrations.manage"] });
      }
      if (path.endsWith("/token-credential/status")) {
        return jsonResponse(emptyStatus);
      }
      if (path.endsWith("/token-credential/") && init?.method === "POST") {
        savedBody = String(init.body);
        return jsonResponse(undefined, 204);
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<TokenTerminalSettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Henüz yapılandırılmadı");

    const [, merchantField, branchField, terminalField, clientIdField, clientSecretField] =
      document.querySelectorAll<HTMLInputElement>("form input");
    await setValue(merchantField, "13e5862b-1328-47dd-887c-d9ca6cb4375c");
    await setValue(branchField, "b81bb869-d45c-43df-a078-9337900ff84e");
    await setValue(terminalField, "AV0000111044");
    await setValue(clientIdField, "cid-example");
    await setValue(clientSecretField, "cs-super-secret-value");

    const submitButton = document.querySelector<HTMLButtonElement>('button[type="submit"], form button')!;
    await act(async () => submitButton.click());
    await act(async () => Promise.resolve());

    expect(savedBody).toContain("AV0000111044");
    expect(savedBody).toContain("cs-super-secret-value");
    expect(document.body.textContent).not.toContain("cs-super-secret-value");
    expect(document.body.textContent).toContain("kaydedildi");
  });

  it("pasting the TokenX Connect QR code auto-fills merchant/branch/terminal id", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["integrations.manage"] });
      }
      if (path.endsWith("/token-credential/status")) {
        return jsonResponse(emptyStatus);
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<TokenTerminalSettings />);
    await act(async () => Promise.resolve());

    const [qrField, merchantField, branchField, terminalField] =
      document.querySelectorAll<HTMLInputElement>("form input");
    await setValue(qrField, "merchant-1_branch-2_AV0000111044");

    expect(merchantField.value).toBe("merchant-1");
    expect(branchField.value).toBe("branch-2");
    expect(terminalField.value).toBe("AV0000111044");
  });

  it("an incomplete QR paste does not overwrite the manually-typed fields", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["integrations.manage"] });
      }
      if (path.endsWith("/token-credential/status")) {
        return jsonResponse(emptyStatus);
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<TokenTerminalSettings />);
    await act(async () => Promise.resolve());

    const [qrField, merchantField] = document.querySelectorAll<HTMLInputElement>("form input");
    await setValue(merchantField, "hand-typed-value");
    await setValue(qrField, "not-a-valid-qr-code");

    expect(merchantField.value).toBe("hand-typed-value");
  });

  it("shows every persisted (non-secret) field when the credential is already configured", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["integrations.manage"] });
      }
      if (path.endsWith("/token-credential/status")) {
        return jsonResponse({
          configured: true, updatedAt: "2026-09-18T10:00:00Z",
          merchantId: "merchant-1", branchId: "branch-2", terminalId: "AV0000111044", clientId: "cid-example",
        });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<TokenTerminalSettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Yapılandırıldı");
    const [, merchantField, branchField, terminalField, clientIdField] =
      document.querySelectorAll<HTMLInputElement>("form input");
    expect(merchantField.value).toBe("merchant-1");
    expect(branchField.value).toBe("branch-2");
    expect(terminalField.value).toBe("AV0000111044");
    expect(clientIdField.value).toBe("cid-example");
  });
});
