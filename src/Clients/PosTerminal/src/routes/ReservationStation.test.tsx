// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ReservationStation } from "./ReservationStation";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

/**
 * V1-CUI-006: the dedicated Reservation Station screen (`/reservations`).
 * Off (the V1-SET-003 default) shows a clear "not enabled" message rather
 * than a broken or empty screen; on, past staff login, it reuses
 * workspace.tsx's TableRoute unchanged.
 */
describe("ReservationStation", () => {
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

  it("with no session, shows the staff login form", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse({ error: { code: "UNAUTHORIZED", message: "Oturum yok." } }, 401)));

    await render(<ReservationStation />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Personel girişi");
  });

  it("with a session but the deployment has not turned the station on, shows the disabled message", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) return jsonResponse({ userId: "u1", displayName: "Ayşe", terminalId: "t1" });
      if (path.includes("/runtime-configuration")) return jsonResponse({ kitchenStationId: "hot-line", reservationStationEnabled: false });
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<ReservationStation />);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Bu ekran etkin değil");
  });

  it("with the station enabled, renders past login into the table workspace", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) return jsonResponse({ userId: "u1", displayName: "Ayşe", terminalId: "t1" });
      if (path.includes("/runtime-configuration")) return jsonResponse({ kitchenStationId: "hot-line", reservationStationEnabled: true });
      if (path.includes("/table-management/zones")) return jsonResponse([]);
      if (path.includes("/table-management/tables")) return jsonResponse([]);
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render(<ReservationStation />);
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Rezervasyon istasyonu");
    expect(document.body.textContent).not.toContain("Personel girişi");
    expect(document.body.textContent).not.toContain("Bu ekran etkin değil");
  });

  // V1-RMD-373 (module-by-module UI audit, 2026-09-27): unlike ~13 other
  // feature workspaces, none of the Ayarlar (settings) screens had an
  // axe-core scan.
  it("has no critical or serious axe violations", async () => {
    document.title = "ALKAROS rezervasyon istasyonu";
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse({ error: { code: "UNAUTHORIZED", message: "Oturum yok." } }, 401)));
    await render(<ReservationStation />);
    await act(async () => Promise.resolve());
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
