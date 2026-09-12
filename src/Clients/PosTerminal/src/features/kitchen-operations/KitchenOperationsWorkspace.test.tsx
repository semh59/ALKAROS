// @vitest-environment jsdom

import { act, type ComponentProps, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { KitchenOperationsWorkspace, type KitchenData } from "./index";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const data: KitchenData = {
  tickets: [{
    id: "ticket-1", orderId: "order-1", ticketNumber: "KT-001", stationId: "hot-line", status: "Preparing", rowVersion: 4,
    createdAt: "2026-08-26T10:00:00Z", updatedAt: "2026-08-26T10:02:00Z", acceptedAt: "2026-08-26T10:01:00Z", readyAt: null, cancelledAt: null, targetPrepMinutes: 15,
    items: [{ id: "item-1", orderItemId: "order-item-1", productId: "product-1", productName: "Mercimek çorbası", quantity: 2, modifiers: "Ekmeği ayrı", notes: "az tuz", status: "Preparing", rowVersion: 2, createdAt: "2026-08-26T10:00:00Z", updatedAt: null, readyAt: null, servedAt: null, cancelledAt: null }],
  }],
  printers: [{ id: "printer-1", name: "Mutfak yazıcı", stationId: "hot-line", isActive: true, createdAt: "2026-08-26T09:00:00Z", updatedAt: null }],
  routes: [{ id: "route-1", routeLevel: "Default", printerId: "printer-1", itemId: null, productId: null, categoryId: null, specialDate: null, isActive: true, createdAt: "2026-08-26T09:00:00Z", updatedAt: null }],
  categories: [{ id: "category-1", name: "Izgara" }],
  unknownDeliveries: [{ id: "delivery-1", printJobId: "job-1", ticketId: "ticket-1", printerId: "printer-1", status: "Unknown", attemptNumber: 1, isReprint: false, operatorReason: null, crashReason: "ACK alınamadı", createdAt: "2026-08-26T10:00:00Z", deliveredAt: null, resolvedAt: null, rowVersion: 1 }],
  health: { snapshotId: "snapshot-1", databaseStatus: "Healthy", diskStatus: "Unhealthy", lastBackupStatus: "Unhealthy", freeDiskBytes: 10, databaseSizeBytes: 100, capturedAt: "2026-08-26T10:00:00Z" },
  backups: [{ backupId: "backup-1", backupType: "Full", fileSizeBytes: 0, status: "Failed", errorMessage: "Backup engine unavailable", startedAt: "2026-08-26T09:00:00Z", completedAt: "2026-08-26T09:01:00Z", retentionDays: 30 }],
};

function baseProps(overrides: Partial<ComponentProps<typeof KitchenOperationsWorkspace>> = {}) {
  return { state: "ready" as const, stationId: "hot-line", data, canOperate: true, canManageReprints: true, onRefresh: vi.fn(), onTransitionItem: vi.fn(), onTransitionTicket: vi.fn(), onApproveReprint: vi.fn(), onRejectReprint: vi.fn(), ...overrides };
}

describe("kitchen operations workspace", () => {
  let root: Root | null = null;
  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.head.innerHTML = "<title>ALKAROS mutfak operasyonu</title>";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }
  async function click(element: Element) { await act(async () => (element as HTMLElement).click()); }
  async function fill(input: HTMLInputElement, value: string) {
    await act(async () => {
      const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;
      setter.call(input, value);
      input.dispatchEvent(new Event("input", { bubbles: true }));
      input.dispatchEvent(new Event("change", { bubbles: true }));
    });
  }
  afterEach(async () => { if (root) await act(async () => root!.unmount()); root = null; vi.restoreAllMocks(); });

  it("renders station tickets, item action without customer detail", async () => {
    const onTransitionItem = vi.fn();
    await render(<KitchenOperationsWorkspace {...baseProps({ onTransitionItem })} />);
    expect(document.body.textContent).toContain("KT-001");
    expect(document.body.textContent).toContain("Mercimek çorbası");
    expect(document.body.textContent).not.toContain("customer");
    await click([...document.querySelectorAll("button")].find((button) => button.textContent?.includes("→ Hazır"))!);
    expect(onTransitionItem).toHaveBeenCalledWith(data.tickets[0], data.tickets[0].items[0], "Ready");
  });

  it("requires a supervisor reason before resolving Unknown delivery", async () => {
    const onApproveReprint = vi.fn();
    await render(<KitchenOperationsWorkspace {...baseProps({ onApproveReprint })} />);
    await click([...document.querySelectorAll("button")].find((button) => button.textContent?.includes("Gerekçeli onay"))!);
    const dialog = document.querySelector('[role="dialog"]')!;
    await click([...dialog.querySelectorAll("button")].find((button) => button.textContent?.includes("Reprint'i onayla"))!);
    expect(dialog.textContent).toContain("Süpervizör gerekçesi zorunlu.");
    const input = dialog.querySelector<HTMLInputElement>("input")!;
    await fill(input, "İstasyonda fiziksel kontrol yapıldı; ticket çıkmadı.");
    await click([...dialog.querySelectorAll("button")].find((button) => button.textContent?.includes("Reprint'i onayla"))!);
    expect(onApproveReprint).toHaveBeenCalledWith(data.unknownDeliveries[0], "İstasyonda fiziksel kontrol yapıldı; ticket çıkmadı.");
  });

  it.each([
    ["loading", "Mutfak yükleniyor"], ["busy", "Mutfak güncelleniyor"], ["offline", "Bağlantı yok"],
    ["unauthorized", "Kasiyer oturumu gerekli"], ["error", "Mutfak verisi alınamadı"], ["stale", "Mutfak verisi güncel değil"], ["conflict", "Mutfak çakışması"],
  ] as const)("shows bounded %s state", async (state, title) => {
    await render(<KitchenOperationsWorkspace {...baseProps({ state, errorMessage: "API kapalı" })} />);
    expect(document.body.textContent).toContain(title);
  });

  it("does not leak the internal Unknown type name in visible text", async () => {
    await render(<KitchenOperationsWorkspace {...baseProps()} />);
    expect(document.body.textContent).not.toContain("Unknown");
    expect(document.body.textContent).toContain("Doğrulanamayan teslimatlar");
    expect(document.body.textContent).toContain("Doğrulanamayan baskı");
    expect(document.body.textContent).not.toContain("Healthy");
    expect(document.body.textContent).not.toContain("Unhealthy");
  });

  it("shows the line special instruction on the ticket", async () => {
    await render(<KitchenOperationsWorkspace {...baseProps()} />);
    const note = document.querySelector(".kitchen-ticket__item-note");
    expect(note).not.toBeNull();
    expect(note!.textContent).toContain("az tuz");
  });

  it("escalates ticket age colour past the station preparation threshold", async () => {
    await render(<KitchenOperationsWorkspace {...baseProps()} />);
    const age = document.querySelector(".kitchen-ticket__age");
    expect(age).not.toBeNull();
    expect(age!.className).toContain("kitchen-ticket__age--crit");
  });

  it("keeps the detailed health panel out of the operator view but shows the top-bar dot", async () => {
    await render(<KitchenOperationsWorkspace {...baseProps({ canManageReprints: false })} />);
    expect(document.querySelector(".kitchen-workspace__admin-health")).toBeNull();
    expect(document.body.textContent).not.toContain("Sağlık ve yedek");
    const dot = document.querySelector(".kitchen-workspace__header-actions .kitchen-health-dot");
    expect(dot).not.toBeNull();
    expect(dot!.getAttribute("aria-label")).toContain("Sistem durumu");
  });

  it("has no critical or serious axe violations", async () => {
    await render(<KitchenOperationsWorkspace {...baseProps()} />);
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
