// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { BusinessIdentitySettings } from "./BusinessIdentitySettings";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: body === undefined ? {} : { "Content-Type": "application/json" },
  });

const PALETTE = {
  entries: [
    { key: "lacivert", label: "Lacivert", hex: "#1B4D7B" },
    { key: "bordo", label: "Bordo", hex: "#8C2F39" },
  ],
  defaultKey: "lacivert",
};

/**
 * V1-CUI-012: unlike CustomerDisplayScreensaverSettings.tsx (untested here,
 * no equivalent file exists), this screen's read endpoints
 * (GET /api/v1/qr/branding, GET /api/v1/qr/logo) are public — every test
 * below proves the form is prefilled from a REAL server response, not left
 * showing only what this session itself just changed.
 */
describe("BusinessIdentitySettings", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  beforeEach(() => {
    // jsdom does not implement these; the component's own logo preview
    // path calls them directly (same reasoning as fetchIdleScreensaver's
    // established use elsewhere in this client).
    (URL as unknown as { createObjectURL: (blob: Blob) => string }).createObjectURL = vi.fn(() => "blob:mock-logo");
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

    await render(<BusinessIdentitySettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Yönetici girişi");
  });

  it("a session without settings.manage sees the forbidden message, not the settings form", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Ayşe", terminalId: "t1", capabilities: ["orders.create"] });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<BusinessIdentitySettings />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("erişim yetkiniz yok");
    expect(document.querySelector('input[placeholder="Örn. Sahil Cafe"]')).toBeNull();
  });

  it("a manager sees the form prefilled with the real current name/color/logo and can save a new name", async () => {
    let savedNamePut: string | undefined;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["settings.manage"] });
      }
      if (path.endsWith("/api/v1/qr/branding")) {
        return jsonResponse({ businessName: "Sahil Cafe", accentColor: "#1B4D7B", hasLogo: false });
      }
      if (path.endsWith("/accent-palette")) {
        return jsonResponse(PALETTE);
      }
      if (path.endsWith("/api/v1/qr/logo")) {
        return jsonResponse(undefined, 404);
      }
      if (path.endsWith("/management/settings/business.name") && (!init || !init.method || init.method === "GET")) {
        return jsonResponse({
          settingId: "s1", key: "business.name", value: "Sahil Cafe", dataType: "Text", scope: "Global",
          moduleOwner: "BusinessIdentity", description: null, requiresRestart: false, active: true,
          updatedAt: "2026-09-19T00:00:00Z", rowVersion: 1,
        });
      }
      if (path.endsWith("/management/settings/business.name") && init?.method === "PUT") {
        savedNamePut = String(init.body);
        return jsonResponse({
          settingId: "s1", key: "business.name", value: "Yeni Ad", dataType: "Text", scope: "Global",
          moduleOwner: "BusinessIdentity", description: null, requiresRestart: false, active: true,
          updatedAt: "2026-09-19T00:01:00Z", rowVersion: 2,
        });
      }
      throw new Error(`unexpected fetch: ${path} ${init?.method ?? "GET"}`);
    }));

    await render(<BusinessIdentitySettings />);
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    const nameInput = document.querySelector<HTMLInputElement>('input[placeholder="Örn. Sahil Cafe"]')!;
    expect(nameInput.value).toBe("Sahil Cafe");
    // The lacivert radio (matching the real #1B4D7B accentColor) is
    // pre-selected — the form never leaves the picker unset when the
    // server already has a real value.
    const lacivertRadio = document.querySelector<HTMLInputElement>('input[value="lacivert"]')!;
    expect(lacivertRadio.checked).toBe(true);

    const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value")!.set!;
    await act(async () => {
      nativeSetter.call(nameInput, "Yeni Ad");
      nameInput.dispatchEvent(new Event("input", { bubbles: true }));
    });

    const nameForm = nameInput.closest("form")!;
    const submitButton = nameForm.querySelector<HTMLButtonElement>("button")!;
    await act(async () => submitButton.click());
    await act(async () => Promise.resolve());

    expect(savedNamePut).toContain("Yeni Ad");
    expect(savedNamePut).toContain('"expectedRowVersion":1');
    expect(document.body.textContent).toContain("kaydedildi");
  });

  it("a manager can pick a different accent color, sent to the server by its key, not its hex", async () => {
    let savedAccentPut: string | undefined;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["settings.manage"] });
      }
      if (path.endsWith("/api/v1/qr/branding")) {
        return jsonResponse({ businessName: "", accentColor: "#1B4D7B", hasLogo: false });
      }
      if (path.endsWith("/accent-palette")) {
        return jsonResponse(PALETTE);
      }
      if (path.endsWith("/api/v1/qr/logo")) {
        return jsonResponse(undefined, 404);
      }
      if (path.endsWith("/management/settings/business.accent_theme") && (!init?.method || init.method === "GET")) {
        return jsonResponse({
          settingId: "s2", key: "business.accent_theme", value: "lacivert", dataType: "Text", scope: "Global",
          moduleOwner: "BusinessIdentity", description: null, requiresRestart: false, active: true,
          updatedAt: "2026-09-19T00:00:00Z", rowVersion: 1,
        });
      }
      if (path.endsWith("/management/settings/business.accent_theme") && init?.method === "PUT") {
        savedAccentPut = String(init.body);
        return jsonResponse({
          settingId: "s2", key: "business.accent_theme", value: "bordo", dataType: "Text", scope: "Global",
          moduleOwner: "BusinessIdentity", description: null, requiresRestart: false, active: true,
          updatedAt: "2026-09-19T00:01:00Z", rowVersion: 2,
        });
      }
      throw new Error(`unexpected fetch: ${path} ${init?.method ?? "GET"}`);
    }));

    await render(<BusinessIdentitySettings />);
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    const bordoRadio = document.querySelector<HTMLInputElement>('input[value="bordo"]')!;
    await act(async () => bordoRadio.click());

    const accentForm = bordoRadio.closest("form")!;
    const submitButton = accentForm.querySelector<HTMLButtonElement>("button")!;
    await act(async () => submitButton.click());
    await act(async () => Promise.resolve());

    expect(savedAccentPut).toContain('"newValue":"bordo"');
    expect(savedAccentPut).not.toContain("#8C2F39");
    expect(document.body.textContent).toContain("kaydedildi");
  });

  it("a manager can upload a logo (real multipart PUT) and then remove it (real DELETE)", async () => {
    let uploadedFormData: FormData | undefined;
    let deleteCalled = false;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["settings.manage"] });
      }
      if (path.endsWith("/api/v1/qr/branding")) {
        return jsonResponse({ businessName: "", accentColor: "#1B4D7B", hasLogo: false });
      }
      if (path.endsWith("/accent-palette")) {
        return jsonResponse(PALETTE);
      }
      if (path.endsWith("/api/v1/qr/logo") && (!init || !init.method || init.method === "GET")) {
        return jsonResponse(undefined, 404);
      }
      if (path.endsWith("/management/business-identity/logo") && init?.method === "PUT") {
        uploadedFormData = init.body as FormData;
        return jsonResponse(undefined, 204);
      }
      if (path.endsWith("/management/business-identity/logo") && init?.method === "DELETE") {
        deleteCalled = true;
        return jsonResponse(undefined, 204);
      }
      throw new Error(`unexpected fetch: ${path} ${init?.method ?? "GET"}`);
    }));

    await render(<BusinessIdentitySettings />);
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    const file = new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], "logo.png", { type: "image/png" });
    const fileInput = document.querySelector<HTMLInputElement>('input[type="file"]')!;
    await act(async () => {
      Object.defineProperty(fileInput, "files", { value: [file], configurable: true });
      fileInput.dispatchEvent(new Event("change", { bubbles: true }));
    });

    const uploadForm = fileInput.closest("form")!;
    const uploadButton = uploadForm.querySelector<HTMLButtonElement>("button")!;
    await act(async () => uploadButton.click());
    await act(async () => Promise.resolve());

    expect(uploadedFormData).toBeInstanceOf(FormData);
    expect(uploadedFormData!.get("file")).toBe(file);
    expect(document.body.textContent).toContain("Logo yüklendi");

    const removeButton = Array.from(document.querySelectorAll<HTMLButtonElement>("button"))
      .find((button) => button.textContent === "Kaldır")!;
    await act(async () => removeButton.click());
    expect(deleteCalled).toBe(false);
    const confirmButton = Array.from(document.querySelectorAll<HTMLButtonElement>("button"))
      .find((button) => button.textContent === "Evet, kaldır")!;
    await act(async () => confirmButton.click());
    await act(async () => Promise.resolve());

    expect(deleteCalled).toBe(true);
    expect(document.body.textContent).toContain("Logo kaldırıldı");
  });

  const nameRecord = (rowVersion: number) => ({
    settingId: "s1", key: "business.name", value: "Sahil Cafe", dataType: "Text", scope: "Global",
    moduleOwner: "BusinessIdentity", description: null, requiresRestart: false, active: true,
    updatedAt: "2026-09-19T00:00:00Z", rowVersion,
  });

  /** A manager session with the identity read endpoints answering; `extra` handles whatever a test wants to vary. */
  function managerFetch(extra: (path: string, init?: RequestInit) => Response | undefined) {
    return vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      const custom = extra(path, init);
      if (custom) return custom;
      if (path.includes("/auth/session")) return jsonResponse({ userId: "u1", displayName: "Zeynep", terminalId: "t1", capabilities: ["settings.manage"] });
      if (path.endsWith("/api/v1/qr/branding")) return jsonResponse({ businessName: "Sahil Cafe", accentColor: "#1B4D7B", hasLogo: false });
      if (path.endsWith("/accent-palette")) return jsonResponse(PALETTE);
      if (path.endsWith("/api/v1/qr/logo")) return jsonResponse(undefined, 404);
      throw new Error(`unexpected fetch: ${path} ${init?.method ?? "GET"}`);
    });
  }

  async function saveName() {
    const nameInput = document.querySelector<HTMLInputElement>('input[placeholder="Örn. Sahil Cafe"]')!;
    const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value")!.set!;
    await act(async () => {
      nativeSetter.call(nameInput, "Yeni Ad");
      nameInput.dispatchEvent(new Event("input", { bubbles: true }));
    });
    await act(async () => nameInput.closest("form")!.querySelector<HTMLButtonElement>("button")!.click());
    await act(async () => Promise.resolve());
  }

  const flush = async () => { await act(async () => Promise.resolve()); await act(async () => Promise.resolve()); };

  it("saves with the version the screen loaded, so a change made by someone else in between is caught", async () => {
    let nameReads = 0;
    let put: string | undefined;
    vi.stubGlobal("fetch", managerFetch((path, init) => {
      if (!path.endsWith("/management/settings/business.name")) return undefined;
      if (init?.method === "PUT") { put = String(init.body); return jsonResponse({ ...nameRecord(8), value: "Yeni Ad" }); }
      nameReads += 1;
      return jsonResponse(nameRecord(nameReads === 1 ? 7 : 99));
    }));
    await render(<BusinessIdentitySettings />);
    await flush();
    await saveName();
    expect(put).toContain('"expectedRowVersion":7');
    expect(nameReads).toBe(1);
  });

  it("when someone else changed the name meanwhile it says so in Turkish and reloads the screen", async () => {
    let brandingReads = 0;
    vi.stubGlobal("fetch", managerFetch((path, init) => {
      if (path.endsWith("/api/v1/qr/branding")) { brandingReads += 1; return jsonResponse({ businessName: "Sahil Cafe", accentColor: "#1B4D7B", hasLogo: false }); }
      if (!path.endsWith("/management/settings/business.name")) return undefined;
      if (init?.method === "PUT") return jsonResponse({ error: { code: "CONCURRENCY_CONFLICT", message: "Ayar başka bir işlem tarafından değiştirildi." } }, 409);
      return jsonResponse(nameRecord(1));
    }));
    await render(<BusinessIdentitySettings />);
    await flush();
    await saveName();
    await flush();
    expect(document.body.textContent).toContain("Ayar başka biri tarafından değiştirilmiş; ekran yenilendi, tekrar deneyin.");
    expect(brandingReads).toBe(2);
  });

  it("when the session ended during a save it goes back to the login form instead of staying stuck", async () => {
    vi.stubGlobal("fetch", managerFetch((path, init) => {
      if (!path.endsWith("/management/settings/business.name")) return undefined;
      if (init?.method === "PUT") return jsonResponse({ error: { code: "UNAUTHORIZED", message: "Oturum geçersiz." } }, 401);
      return jsonResponse(nameRecord(1));
    }));
    await render(<BusinessIdentitySettings />);
    await flush();
    await saveName();
    await flush();
    expect(document.body.textContent).toContain("Yönetici girişi");
    expect(document.body.textContent).toContain("Oturum sona erdi; yeniden giriş yapın.");
  });

  it("when the current settings cannot be read it offers a retry that unlocks the form", async () => {
    let brandingFails = true;
    vi.stubGlobal("fetch", managerFetch((path) => {
      if (path.endsWith("/api/v1/qr/branding") && brandingFails) return jsonResponse({ error: { code: "X", message: "Sunucu yanıt vermedi." } }, 500);
      return undefined;
    }));
    await render(<BusinessIdentitySettings />);
    await flush();
    expect(document.querySelector<HTMLInputElement>('input[placeholder="Örn. Sahil Cafe"]')!.disabled).toBe(true);
    expect(document.body.textContent).toContain("Sunucu yanıt vermedi.");

    brandingFails = false;
    await act(async () => Array.from(document.querySelectorAll<HTMLButtonElement>("button")).find((button) => button.textContent === "Tekrar dene")!.click());
    await flush();
    expect(document.querySelector<HTMLInputElement>('input[placeholder="Örn. Sahil Cafe"]')!.disabled).toBe(false);
    expect(document.body.textContent).not.toContain("Tekrar dene");
  });

  it("asks before removing the logo and keeps it when the manager steps back", async () => {
    let deleteCalled = false;
    vi.stubGlobal("fetch", managerFetch((path, init) => {
      if (path.endsWith("/api/v1/qr/logo")) return new Response(new Uint8Array([1, 2, 3]), { status: 200, headers: { "Content-Type": "image/png" } });
      if (path.endsWith("/management/business-identity/logo") && init?.method === "DELETE") { deleteCalled = true; return jsonResponse(undefined, 204); }
      return undefined;
    }));
    await render(<BusinessIdentitySettings />);
    await flush();
    const byText = (text: string) => Array.from(document.querySelectorAll<HTMLButtonElement>("button")).find((button) => button.textContent === text)!;
    await act(async () => byText("Kaldır").click());
    expect(document.body.textContent).toContain("Emin misiniz?");
    await act(async () => byText("Vazgeç").click());
    expect(deleteCalled).toBe(false);
    expect(document.body.textContent).not.toContain("Emin misiniz?");
    expect(byText("Kaldır")).toBeDefined();
  });

  // V1-RMD-373 (module-by-module UI audit, 2026-09-27): unlike ~13 other
  // feature workspaces, none of the Ayarlar (settings) screens had an
  // axe-core scan.
  it("has no critical or serious axe violations", async () => {
    document.title = "ALKAROS işletme kimliği";
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse({ error: { code: "UNAUTHORIZED", message: "Oturum yok." } }, 401)));
    await render(<BusinessIdentitySettings />);
    await act(async () => Promise.resolve());
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
