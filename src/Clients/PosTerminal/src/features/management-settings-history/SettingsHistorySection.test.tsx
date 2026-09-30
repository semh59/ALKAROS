// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { createSettingsClient, type SettingsClient } from "./api";
import { SettingsHistorySection } from "./index";
import { alertText, buttons, press, render, unmount } from "../management/testKit";
import { ManagementApiError } from "../management/http";

const toggle = { settingId: "s1", key: "kitchen.live_sync_enabled", value: "true", dataType: "Toggle", requiresRestart: true, active: true, updatedAt: "2026-09-30T08:00:00Z", rowVersion: 3 };
const name = { settingId: "s2", key: "business.name", value: "Lokanta", dataType: "Text", requiresRestart: false, active: false, updatedAt: "2026-09-29T08:00:00Z", rowVersion: 1 };
const entry = (id: string, changedAt: string, oldValue: string | null, newValue: string, changedBy: string | null) => ({ settingHistoryId: id, oldValue, newValue, reason: "Vardiya değişti", changedBy, changedAt });

function fakeClient(overrides: Partial<SettingsClient> = {}): SettingsClient {
  return {
    listSettings: vi.fn().mockResolvedValue([toggle, name]),
    history: vi.fn().mockResolvedValue([
      { ...entry("h1", "2026-09-29T08:00:00Z", null, "false", null), reason: "Initial registration" },
      entry("h2", "2026-09-30T08:00:00Z", "false", "true", "abcdef12-0000-0000-0000-000000000000"),
    ]),
    ...overrides,
  };
}

describe("settings history section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("the shell lists the settings history tab only for a session holding settings.manage", async () => {
    await render(<ManagementArea capabilities={new Set(["reports.view"])} />);
    expect(buttons()).not.toContain("Ayar geçmişi");
    await render(<ManagementArea capabilities={new Set(["reports.view", "settings.manage"])} />);
    expect(buttons()).toContain("Ayar geçmişi");
  });

  it("lists settings with Turkish names and values, never the raw key or boolean", async () => {
    await render(<SettingsHistorySection client={fakeClient()} />);
    const text = document.body.textContent!;
    expect(text).toContain("Mutfak canlı eşitleme");
    expect(text).toContain("Evet");
    expect(text).toContain("Yeniden başlatma gerekir");
    expect(text).toContain("Pasif");
    expect(text).not.toContain("kitchen.live_sync_enabled");
    expect(text).not.toContain("true");
  });

  it("an unknown setting key gets a neutral Turkish name", async () => {
    const client = fakeClient({ listSettings: vi.fn().mockResolvedValue([{ ...name, key: "future.thing" }]) });
    await render(<SettingsHistorySection client={client} />);
    expect(document.body.textContent).toContain("Tanımsız ayar");
    expect(document.body.textContent).not.toContain("future.thing");
  });

  it("shows the history newest first with who, before and after, and the reason", async () => {
    const client = fakeClient();
    await render(<SettingsHistorySection client={client} />);
    await press("Geçmişi göster");
    expect(client.history).toHaveBeenCalledWith("kitchen.live_sync_enabled");
    const rows = [...document.querySelectorAll('section[aria-label^="Değişiklik geçmişi"] tbody tr')].map((row) => row.textContent);
    expect(rows).toHaveLength(2);
    expect(rows[0]).toContain("Kullanıcı abcdef12");
    expect(rows[0]).toContain("HayırEvet");
    expect(rows[0]).toContain("Vardiya değişti");
    expect(rows[1]).toContain("Sistem");
    expect(rows[1]).toContain("İlk kayıt");
    expect(rows[1]).not.toContain("Initial registration");
    expect(rows[1]).toContain("—");
  });

  it("says so when a setting has no recorded change", async () => {
    await render(<SettingsHistorySection client={fakeClient({ history: vi.fn().mockResolvedValue([]) })} />);
    await press("Geçmişi göster");
    expect(document.body.textContent).toContain("Bu ayar için kayıtlı değişiklik yok.");
  });

  it("every setting the server ships by default has a Turkish name", async () => {
    const keys = [
      "qr_ordering.pending_confirmation_timeout", "reservations.dedicated_station_enabled", "garson.course_management_enabled",
      "garson.guest_live_bill_enabled", "garson.help_request_enabled", "garson.party_size_enabled", "garson.personal_comp_budget_enabled",
      "garson.seat_assignment_enabled", "garson.shift_handoff_notes_enabled", "garson.shift_summary_enabled", "garson.voluntary_tip_enabled",
    ];
    const client = fakeClient({ listSettings: vi.fn().mockResolvedValue(keys.map((key, index) => ({ ...toggle, key, settingId: `k${index}` }))) });
    await render(<SettingsHistorySection client={client} />);
    expect(document.body.textContent).not.toContain("Tanımsız ayar");
  });

  it("has no action that changes a setting", async () => {
    await render(<SettingsHistorySection client={fakeClient()} />);
    await press("Geçmişi göster");
    expect(new Set(buttons())).toEqual(new Set(["Geçmişi göster"]));
  });

  it("shows the Turkish reason from the server when the list cannot be read", async () => {
    const client = fakeClient({ listSettings: vi.fn().mockRejectedValue(new ManagementApiError(403, "FORBIDDEN", "Ayar yönetimi izni gerekiyor.")) });
    await render(<SettingsHistorySection client={client} />);
    expect(document.body.textContent).toContain("Ayar yönetimi izni gerekiyor.");
    expect(alertText()).toContain("Ayar yönetimi izni gerekiyor.");
  });

  it("the client encodes the key in the history address", async () => {
    const fetcher = vi.fn(() => Promise.resolve(new Response("[]", { status: 200 })));
    await createSettingsClient(fetcher as unknown as typeof fetch).history("a b/c");
    expect((fetcher.mock.calls[0] as unknown[])[0]).toBe("/api/v1/management/settings/a%20b%2Fc/history");
  });
});
