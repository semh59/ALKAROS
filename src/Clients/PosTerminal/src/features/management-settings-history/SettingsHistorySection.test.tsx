// @vitest-environment jsdom

import { act } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { createSettingsClient, type SettingsClient } from "./api";
import { SettingsHistorySection } from "./index";
import { alertText, buttons, press, render, submit, type as typeInto, unmount } from "../management/testKit";
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
    update: vi.fn().mockResolvedValue({ ...toggle, rowVersion: 4 }),
    ...overrides,
  };
}

const waiterLimit = { settingId: "s3", key: "waiter.max_active_tables", value: "0", dataType: "WholeNumber", requiresRestart: false, active: true, updatedAt: "2026-09-30T08:00:00Z", rowVersion: 1 };
const qrTimeout = { settingId: "s4", key: "qr_ordering.pending_confirmation_timeout", value: "00:05:00", dataType: "Duration", requiresRestart: false, active: true, updatedAt: "2026-09-30T08:00:00Z", rowVersion: 2 };
const idOfField = (label: string) => ([...document.querySelectorAll("label.ds-field")].find((field) => field.textContent?.includes(label)) as HTMLLabelElement).htmlFor;

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
    expect(rows[0]).toContain("Bilinmeyen personel");
    expect(rows[0]).not.toContain("abcdef12");
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

  it("offers editing only for an active setting the screen knows how to edit", async () => {
    await render(<SettingsHistorySection client={fakeClient({ listSettings: vi.fn().mockResolvedValue([toggle, name, waiterLimit, { ...toggle, key: "future.thing", dataType: "Text", settingId: "s9" }]) })} />);
    expect(buttons().filter((label) => label === "Düzenle")).toHaveLength(2);
  });

  it("shows the name of whoever changed a setting and never an id fragment", async () => {
    const client = fakeClient({ history: vi.fn().mockResolvedValue([
      { ...entry("h1", "2026-09-30T08:00:00Z", "false", "true", "abcdef12-0000-0000-0000-000000000000"), changedByName: "Ayşe Yıldız" },
      entry("h2", "2026-09-29T08:00:00Z", "true", "false", "99999999-0000-0000-0000-000000000000"),
    ]) });
    await render(<SettingsHistorySection client={client} />);
    await press("Geçmişi göster");
    const rows = [...document.querySelectorAll('section[aria-label^="Değişiklik geçmişi"] tbody tr')].map((row) => row.textContent);
    expect(rows[0]).toContain("Ayşe Yıldız");
    expect(rows[1]).toContain("Bilinmeyen personel");
    expect(document.body.textContent).not.toMatch(/abcdef12|99999999/);
  });

  it("shows times in Istanbul time and translates the deactivation record", async () => {
    const client = fakeClient({ history: vi.fn().mockResolvedValue([
      { ...entry("h1", "2026-09-29T08:00:00Z", "true", "[Deactivated]", null), reason: "Setting deactivated" },
    ]) });
    await render(<SettingsHistorySection client={client} />);
    await press("Geçmişi göster");
    const row = document.querySelector('section[aria-label^="Değişiklik geçmişi"] tbody tr')!.textContent!;
    expect(row).toContain("29.09.2026 11:00");
    expect(row).toContain("Pasifleştirildi");
    expect(row).toContain("Ayar pasifleştirildi");
    expect(row).not.toContain("Deactivated");
  });

  it("writes durations and the table limit in words with their unit", async () => {
    await render(<SettingsHistorySection client={fakeClient({ listSettings: vi.fn().mockResolvedValue([waiterLimit, qrTimeout]) })} />);
    expect(document.body.textContent).toContain("Sınırsız");
    expect(document.body.textContent).toContain("5 dakika");
    expect(document.body.textContent).not.toContain("00:05:00");
  });

  it("changes the waiter table limit with a reason and reloads the list", async () => {
    const client = fakeClient({ listSettings: vi.fn().mockResolvedValue([waiterLimit]) });
    await render(<SettingsHistorySection client={client} />);
    await press("Düzenle");
    await typeInto(idOfField("Yeni değer"), "3");
    await typeInto(idOfField("Değişiklik gerekçesi"), "Yoğun akşam");
    await submit("Ayarı değiştir: Garson başına en fazla açık masa");
    expect(client.update).toHaveBeenCalledWith("waiter.max_active_tables", "3", 1, "Yoğun akşam");
    expect(document.body.textContent).toContain("Ayar kaydedildi.");
    expect(client.listSettings).toHaveBeenCalledTimes(2);
    expect(document.querySelector("form")).toBeNull();
  });

  it("sends a duration in minutes as the server's time format", async () => {
    const client = fakeClient({ listSettings: vi.fn().mockResolvedValue([qrTimeout]) });
    await render(<SettingsHistorySection client={client} />);
    await press("Düzenle");
    expect((document.getElementById(idOfField("Yeni değer (dakika)")) as HTMLInputElement).value).toBe("5");
    await typeInto(idOfField("Yeni değer (dakika)"), "90");
    await typeInto(idOfField("Değişiklik gerekçesi"), "Uzun bekleme");
    await submit("Ayarı değiştir: QR siparişi onay bekleme süresi");
    expect(client.update).toHaveBeenCalledWith("qr_ordering.pending_confirmation_timeout", "01:30:00", 2, "Uzun bekleme");
  });

  it("changes a toggle with the Evet and Hayır choice", async () => {
    const client = fakeClient();
    await render(<SettingsHistorySection client={client} />);
    await press("Düzenle");
    await typeInto(idOfField("Yeni değer"), "false");
    await typeInto(idOfField("Değişiklik gerekçesi"), "Kapatıldı");
    await submit("Ayarı değiştir: Mutfak canlı eşitleme");
    expect(client.update).toHaveBeenCalledWith("kitchen.live_sync_enabled", "false", 3, "Kapatıldı");
  });

  it("refuses an empty reason, an unchanged value and an out-of-range number without calling the server", async () => {
    const client = fakeClient({ listSettings: vi.fn().mockResolvedValue([waiterLimit]) });
    await render(<SettingsHistorySection client={client} />);
    await press("Düzenle");
    const form = "Ayarı değiştir: Garson başına en fazla açık masa";

    await typeInto(idOfField("Yeni değer"), "3");
    await submit(form);
    expect(alertText()).toBe("Gerekçe yazmanız gerekir.");

    await typeInto(idOfField("Yeni değer"), "0");
    await typeInto(idOfField("Değişiklik gerekçesi"), "x");
    await submit(form);
    expect(alertText()).toBe("Değer değişmedi.");

    await typeInto(idOfField("Yeni değer"), "51");
    await submit(form);
    expect(alertText()).toBe("0 ile 50 arası; 0 = sınırsız");

    await typeInto(idOfField("Yeni değer"), "2.5");
    await submit(form);
    expect(alertText()).toBe("Geçerli bir tam sayı girin.");
    expect(client.update).not.toHaveBeenCalled();
  });

  it("shows the server's Turkish refusal and keeps the editor open", async () => {
    const client = fakeClient({
      listSettings: vi.fn().mockResolvedValue([waiterLimit]),
      update: vi.fn().mockRejectedValue(new ManagementApiError(400, "VALUE_OUT_OF_RANGE", "Garson başına masa sınırı 0 ile 50 arasında olmalı (0 = sınırsız).")),
    });
    await render(<SettingsHistorySection client={client} />);
    await press("Düzenle");
    await typeInto(idOfField("Yeni değer"), "3");
    await typeInto(idOfField("Değişiklik gerekçesi"), "Deneme");
    await submit("Ayarı değiştir: Garson başına en fazla açık masa");
    expect(alertText()).toContain("0 ile 50 arasında olmalı");
    expect(document.querySelector("form")).not.toBeNull();
  });

  it("when someone else changed the setting meanwhile, closes the editor, says so and reloads", async () => {
    const client = fakeClient({
      listSettings: vi.fn().mockResolvedValue([waiterLimit]),
      update: vi.fn().mockRejectedValue(new ManagementApiError(409, "CONCURRENCY_CONFLICT", "Ayar başka bir işlem tarafından değiştirildi.")),
    });
    await render(<SettingsHistorySection client={client} />);
    await press("Düzenle");
    await typeInto(idOfField("Yeni değer"), "3");
    await typeInto(idOfField("Değişiklik gerekçesi"), "Deneme");
    await submit("Ayarı değiştir: Garson başına en fazla açık masa");
    expect(document.querySelector("form")).toBeNull();
    expect(document.body.textContent).toContain("Ayar başka biri tarafından değiştirilmiş; liste yenilendi, tekrar deneyin.");
    expect(client.listSettings).toHaveBeenCalledTimes(2);
  });

  it("a slow history answer for the first setting never overwrites the one chosen after it", async () => {
    let releaseFirst: (value: readonly never[]) => void = () => undefined;
    const slow = new Promise<readonly never[]>((resolve) => { releaseFirst = resolve; });
    const history = vi.fn((key: string) => key === "kitchen.live_sync_enabled"
      ? slow
      : Promise.resolve([{ ...entry("h9", "2026-09-30T08:00:00Z", "0", "3", null), reason: "Sınır konuldu" }]));
    await render(<SettingsHistorySection client={fakeClient({ listSettings: vi.fn().mockResolvedValue([toggle, waiterLimit]), history })} />);
    const showButtons = () => [...document.querySelectorAll("button")].filter((button) => button.textContent === "Geçmişi göster");
    await act(async () => showButtons()[0].click());
    await act(async () => showButtons()[1].click());
    await act(async () => releaseFirst([]));
    expect(document.body.textContent).toContain("Sınır konuldu");
    expect(document.body.textContent).not.toContain("Bu ayar için kayıtlı değişiklik yok.");
  });

  it("shows the Turkish reason from the server when the list cannot be read", async () => {
    const client = fakeClient({ listSettings: vi.fn().mockRejectedValue(new ManagementApiError(403, "FORBIDDEN", "Ayar yönetimi izni gerekiyor.")) });
    await render(<SettingsHistorySection client={client} />);
    expect(document.body.textContent).toContain("Ayar yönetimi izni gerekiyor.");
    expect(alertText()).toContain("Ayar yönetimi izni gerekiyor.");
  });

  it("when the same history is read again after a save, a slow older answer never replaces the newer one", async () => {
    let releaseOld: (value: unknown[]) => void = () => undefined;
    const old = new Promise<unknown[]>((resolve) => { releaseOld = resolve; });
    const history = vi.fn()
      .mockReturnValueOnce(old)
      .mockResolvedValue([{ ...entry("h2", "2026-09-30T09:00:00Z", "true", "false", null), reason: "Yeni neden" }]);
    await render(<SettingsHistorySection client={fakeClient({ history })} />);
    await press("Geçmişi göster");
    await press("Düzenle");
    await typeInto(idOfField("Yeni değer"), "false");
    await typeInto(idOfField("Değişiklik gerekçesi"), "Kapatıldı");
    await submit("Ayarı değiştir: Mutfak canlı eşitleme");
    await act(async () => releaseOld([{ ...entry("h1", "2026-09-29T08:00:00Z", null, "true", null), reason: "Eski neden" }]));
    expect(document.body.textContent).toContain("Yeni neden");
    expect(document.body.textContent).not.toContain("Eski neden");
  });

  it("the client sends a change as a PUT with the version it read", async () => {
    const fetcher = vi.fn(() => Promise.resolve(new Response(JSON.stringify(toggle), { status: 200 })));
    await createSettingsClient(fetcher as unknown as typeof fetch).update("a b", "5", 7, "neden");
    const [url, init] = fetcher.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe("/api/v1/management/settings/a%20b");
    expect(init.method).toBe("PUT");
    expect(JSON.parse(init.body as string)).toEqual({ newValue: "5", expectedRowVersion: 7, reason: "neden" });
  });

  it("the client encodes the key in the history address", async () => {
    const fetcher = vi.fn(() => Promise.resolve(new Response("[]", { status: 200 })));
    await createSettingsClient(fetcher as unknown as typeof fetch).history("a b/c");
    expect((fetcher.mock.calls[0] as unknown[])[0]).toBe("/api/v1/management/settings/a%20b%2Fc/history");
  });
});
