// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RelaySettings } from "./RelaySettings";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: body === undefined ? {} : { "Content-Type": "application/json" },
  });

/**
 * V14-QRT-003: the `/settings/relay` screen — manager-only, per
 * `integrations.manage`, and the saved token must never come back from
 * either the save call's own response or a later status read.
 */
describe("RelaySettings", () => {
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

    await render(<RelaySettings />);
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

    await render(<RelaySettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("erişim yetkiniz yok");
    expect(document.body.textContent).not.toContain("Bağlantı anahtarı");
  });

  it("a manager sees the current status and can save a new token, which is never echoed back", async () => {
    let savedBody: string | undefined;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["integrations.manage"] });
      }
      if (path.endsWith("/relay-credential/status")) {
        return jsonResponse({ configured: false, updatedAt: null, accountId: null, zoneId: null, baseDomain: null });
      }
      if (path.endsWith("/relay-credential/") && init?.method === "POST") {
        savedBody = String(init.body);
        return jsonResponse(undefined, 204);
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<RelaySettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Henüz yapılandırılmadı");

    const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value")!.set!;
    const setValue = async (input: HTMLInputElement, value: string) => {
      await act(async () => {
        nativeSetter.call(input, value);
        input.dispatchEvent(new Event("input", { bubbles: true }));
      });
    };
    const [tokenField, accountField, zoneField, domainField] = document.querySelectorAll<HTMLInputElement>("form input");
    await setValue(tokenField, "cf-super-secret-token");
    await setValue(accountField, "account-abc");
    await setValue(zoneField, "zone-xyz");
    await setValue(domainField, "alkaros.app");

    const submitButton = document.querySelector<HTMLButtonElement>('button[type="submit"], form button')!;
    await act(async () => submitButton.click());
    await act(async () => Promise.resolve());

    expect(savedBody).toContain("cf-super-secret-token");
    expect(savedBody).toContain("account-abc");
    expect(document.body.textContent).not.toContain("cf-super-secret-token");
    expect(document.body.textContent).toContain("kaydedildi");
  });
});
