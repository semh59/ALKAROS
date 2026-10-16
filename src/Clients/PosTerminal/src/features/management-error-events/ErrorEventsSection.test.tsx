// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementApiError } from "../management/http";
import { alertText, buttons, press, render, type, unmount } from "../management/testKit";
import type { ErrorEventsClient } from "./api";
import { ErrorEventsSection } from "./index";

const item = {
  fingerprint: "f1", layer: "Printer", severity: "Critical", category: "printer.offline", status: "Open",
  occurrenceCount: 4, firstSeenAt: "2026-10-16T08:00:00Z", lastSeenAt: "2026-10-16T09:00:00Z", terminalLabel: "terminal:ab12cd34", synthetic: false,
};
const page = (items = [item]) => ({ items, total: items.length, page: 1, pageSize: 25 });

function fakeClient(overrides: Partial<ErrorEventsClient> = {}): ErrorEventsClient {
  return {
    list: vi.fn().mockResolvedValue(page()),
    detail: vi.fn().mockResolvedValue({ event: item, recent: [{ occurredAt: "2026-10-16T09:00:00Z", severity: "Critical", occurrences: 2 }] }),
    channelConfigured: vi.fn().mockResolvedValue(false),
    change: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  };
}

const manager = new Set(["reports.view", "observability.manage"]);

describe("error events section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("shows a loading message before the list arrives", async () => {
    await render(<ErrorEventsSection capabilities={manager} client={fakeClient({ list: () => new Promise(() => undefined) })} />);
    expect(document.body.textContent).toContain("Yükleniyor…");
  });

  it("shows Turkish labels and never the raw enum or category", async () => {
    await render(<ErrorEventsSection capabilities={manager} client={fakeClient()} />);
    expect(document.body.textContent).toContain("Yazıcı çevrimdışı");
    expect(document.body.textContent).toContain("Kritik");
    for (const raw of ["Critical", "Printer", "printer.offline", "Open"]) expect(document.body.textContent).not.toContain(raw);
  });

  it("shows an empty message when there are no errors", async () => {
    await render(<ErrorEventsSection capabilities={manager} client={fakeClient({ list: vi.fn().mockResolvedValue(page([])) })} />);
    expect(document.body.textContent).toContain("Kayıtlı hata yok.");
  });

  it("shows a retryable error state without the raw failure text", async () => {
    await render(<ErrorEventsSection capabilities={manager} client={fakeClient({ list: vi.fn().mockRejectedValue(new Error("ECONNRESET")) })} />);
    expect(document.body.textContent).toContain("Hata listesi alınamadı.");
    expect(document.body.textContent).not.toContain("ECONNRESET");
    expect(buttons()).toContain("Tekrar dene");
  });

  it("shows the Turkish unauthorized message", async () => {
    const denied = new ManagementApiError(403, "FORBIDDEN", "Bu işlem için yetkiniz yok.");
    await render(<ErrorEventsSection capabilities={manager} client={fakeClient({ list: vi.fn().mockRejectedValue(denied) })} />);
    expect(document.body.textContent).toContain("Bu işlem için yetkiniz yok.");
  });

  it("without observability.manage the status buttons are absent", async () => {
    await render(<ErrorEventsSection capabilities={new Set(["reports.view"])} client={fakeClient()} />);
    for (const action of ["Gördüm", "Çözüldü"]) expect(buttons()).not.toContain(action);
  });

  it("acknowledging calls the client and reports success", async () => {
    const client = fakeClient();
    await render(<ErrorEventsSection capabilities={manager} client={client} />);
    await press("Gördüm");
    expect(client.change).toHaveBeenCalledWith("f1", "acknowledge");
    expect(document.body.textContent).toContain("Hata güncellendi.");
  });

  it("a failed action shows the fixed Turkish message", async () => {
    const client = fakeClient({ change: vi.fn().mockRejectedValue(new Error("boom")) });
    await render(<ErrorEventsSection capabilities={manager} client={client} />);
    await press("Çözüldü");
    expect(alertText()).toBe("İşlem tamamlanamadı.");
  });

  it("changing a filter reloads from page one with that filter", async () => {
    const client = fakeClient();
    await render(<ErrorEventsSection capabilities={manager} client={client} />);
    await type("er-layer", "Printer");
    expect(client.list).toHaveBeenLastCalledWith({ layer: "Printer", severity: "", status: "" }, 1);
  });

  it("the detail view lists recent occurrences", async () => {
    await render(<ErrorEventsSection capabilities={manager} client={fakeClient()} />);
    await press("Ayrıntı");
    expect(document.body.textContent).toContain("Son oluşumlar");
  });

  it("states the notification channel status", async () => {
    await render(<ErrorEventsSection capabilities={manager} client={fakeClient()} />);
    expect(document.body.textContent).toContain("Telegram bildirimi yapılandırılmamış");
  });

  it("an unknown category shows Bilinmiyor, never the raw value", async () => {
    const list = vi.fn().mockResolvedValue(page([{ ...item, category: "zzz.new_kind" }]));
    await render(<ErrorEventsSection capabilities={manager} client={fakeClient({ list })} />);
    expect(document.body.textContent).toContain("Bilinmiyor");
    expect(document.body.textContent).not.toContain("zzz.new_kind");
  });

  it("an acknowledged error offers only the resolve action", async () => {
    const list = vi.fn().mockResolvedValue(page([{ ...item, status: "Acknowledged" }]));
    await render(<ErrorEventsSection capabilities={manager} client={fakeClient({ list })} />);
    expect(buttons()).not.toContain("Gördüm");
    expect(buttons()).toContain("Çözüldü");
  });

  it("changing a filter on a later page returns to page one", async () => {
    const list = vi.fn().mockResolvedValue({ items: [item], total: 60, page: 1, pageSize: 25 });
    await render(<ErrorEventsSection capabilities={manager} client={fakeClient({ list })} />);
    await press("Sonraki");
    expect(list).toHaveBeenLastCalledWith({ layer: "", severity: "", status: "" }, 2);
    await type("er-status", "Open");
    expect(list).toHaveBeenLastCalledWith({ layer: "", severity: "", status: "Open" }, 1);
  });
});
