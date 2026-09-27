// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RelaySettings } from "./RelaySettings";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: body === undefined ? {} : { "Content-Type": "application/json" },
  });

/**
 * V12-QRT-003: the `/settings/relay` screen — manager-only, per
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
        return jsonResponse({
          configured: false, updatedAt: null, accountId: null, zoneId: null, baseDomain: null,
          tunnelHostname: null, tunnelUpdatedAt: null, connectorState: "NotConfigured",
        });
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
    expect(document.body.textContent).not.toContain("Bağlantıyı Etkinleştir");

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

  it("once configured, a manager can enable the connection and sees the resulting hostname", async () => {
    let provisionedBody: string | undefined;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["integrations.manage"] });
      }
      if (path.endsWith("/relay-credential/status")) {
        return jsonResponse({
          configured: true, updatedAt: "2026-09-07T10:00:00Z", accountId: "account-abc", zoneId: "zone-xyz", baseDomain: "alkaros.app",
          tunnelHostname: null, tunnelUpdatedAt: null, connectorState: "NotConfigured",
        });
      }
      if (path.endsWith("/relay-credential/provision") && init?.method === "POST") {
        provisionedBody = String(init.body);
        return jsonResponse({ hostname: "sube1.alkaros.app" });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<RelaySettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Bağlantıyı Etkinleştir");
    expect(document.body.textContent).toContain("Henüz etkinleştirilmedi");

    const provisionForm = document.querySelectorAll("form")[1] as HTMLFormElement;
    const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value")!.set!;
    const subdomainField = provisionForm.querySelector<HTMLInputElement>("input")!;
    await act(async () => {
      nativeSetter.call(subdomainField, "sube1");
      subdomainField.dispatchEvent(new Event("input", { bubbles: true }));
    });

    const provisionButton = provisionForm.querySelector<HTMLButtonElement>("button")!;
    await act(async () => provisionButton.click());
    await act(async () => Promise.resolve());

    expect(provisionedBody).toContain("sube1");
    expect(document.body.textContent).toContain("sube1.alkaros.app");
  });

  it("shows the Turkish connector-state label, never the raw server enum name", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["integrations.manage"] });
      }
      if (path.endsWith("/relay-credential/status")) {
        return jsonResponse({
          configured: true, updatedAt: "2026-09-07T10:00:00Z", accountId: "account-abc", zoneId: "zone-xyz", baseDomain: "alkaros.app",
          tunnelHostname: "sube1.alkaros.app", tunnelUpdatedAt: "2026-09-07T10:05:00Z", connectorState: "Running",
        });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<RelaySettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Bağlayıcı çalışıyor");
    expect(document.body.textContent).not.toContain("Running");
  });

  // V1-RMD-324 (independent 2026-09-26 audit, finding K17): the backend now reports "Unknown" instead of
  // a frozen, no-longer-trustworthy "Running" once the connector container stops refreshing its own
  // status row - this screen must show a real Turkish label for that too, not leak the raw enum name.
  it("shows the Turkish label for an unknown (stale) connector status, never the raw server enum name", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["integrations.manage"] });
      }
      if (path.endsWith("/relay-credential/status")) {
        return jsonResponse({
          configured: true, updatedAt: "2026-09-07T10:00:00Z", accountId: "account-abc", zoneId: "zone-xyz", baseDomain: "alkaros.app",
          tunnelHostname: "sube1.alkaros.app", tunnelUpdatedAt: "2026-09-07T10:05:00Z", connectorState: "Unknown",
        });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<RelaySettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Bağlayıcı durumu bilinmiyor");
    expect(document.body.textContent).not.toContain("Unknown");
  });

  // V1-RMD-373 (module-by-module UI audit, 2026-09-27): unlike ~13 other
  // feature workspaces, none of the Ayarlar (settings) screens had an
  // axe-core scan.
  it("has no critical or serious axe violations", async () => {
    document.title = "ALKAROS relay ayarları";
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse({ error: { code: "UNAUTHORIZED", message: "Oturum yok." } }, 401)));
    await render(<RelaySettings />);
    await act(async () => Promise.resolve());
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
