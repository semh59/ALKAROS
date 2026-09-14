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
    createdAt: "2026-08-26T10:00:00Z", updatedAt: "2026-08-26T10:02:00Z", acceptedAt: "2026-08-26T10:01:00Z", readyAt: null, cancelledAt: null, targetPrepMinutes: 15, tableId: null, tableNumber: null,
    items: [{ id: "item-1", orderItemId: "order-item-1", productId: "product-1", productName: "Mercimek çorbası", quantity: 2, modifiers: "Ekmeği ayrı", notes: "az tuz", status: "Preparing", rowVersion: 2, createdAt: "2026-08-26T10:00:00Z", updatedAt: null, readyAt: null, servedAt: null, cancelledAt: null, isAgeRestricted: false }],
  }],
  printers: [{ id: "printer-1", name: "Mutfak yazıcı", stationId: "hot-line", isActive: true, createdAt: "2026-08-26T09:00:00Z", updatedAt: null }],
  routes: [{ id: "route-1", routeLevel: "Default", printerId: "printer-1", itemId: null, productId: null, categoryId: null, specialDate: null, isActive: true, createdAt: "2026-08-26T09:00:00Z", updatedAt: null }],
  categories: [{ id: "category-1", name: "Izgara" }],
  unknownDeliveries: [{ id: "delivery-1", printJobId: "job-1", ticketId: "ticket-1", printerId: "printer-1", status: "Unknown", attemptNumber: 1, isReprint: false, operatorReason: null, crashReason: "ACK alınamadı", createdAt: "2026-08-26T10:00:00Z", deliveredAt: null, resolvedAt: null, rowVersion: 1 }],
  health: { snapshotId: "snapshot-1", databaseStatus: "Healthy", diskStatus: "Unhealthy", lastBackupStatus: "Unhealthy", freeDiskBytes: 10, databaseSizeBytes: 100, capturedAt: "2026-08-26T10:00:00Z" },
  backups: [{ backupId: "backup-1", backupType: "Full", fileSizeBytes: 0, status: "Failed", errorMessage: "Backup engine unavailable", startedAt: "2026-08-26T09:00:00Z", completedAt: "2026-08-26T09:01:00Z", retentionDays: 30 }],
  liveSyncEnabled: true,
  denseModeThreshold: 9,
};

function baseProps(overrides: Partial<ComponentProps<typeof KitchenOperationsWorkspace>> = {}) {
  return { state: "ready" as const, stationId: "hot-line", data, canAdvance: true, canOperate: true, canManageReprints: true, canSuspendAvailability: true, onRefresh: vi.fn(), onTransitionItem: vi.fn(), onTransitionTicket: vi.fn(), onApproveReprint: vi.fn(), onRejectReprint: vi.fn(), ...overrides };
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

  it("groups tickets by order (Expo view) and advances an item without leaking customer detail", async () => {
    const onTransitionItem = vi.fn();
    await render(<KitchenOperationsWorkspace {...baseProps({ onTransitionItem })} />);
    expect(document.body.textContent).toContain("KT-001");
    expect(document.body.textContent).toContain("Mercimek çorbası");
    expect(document.body.textContent).not.toContain("customer");
    const next = document.querySelector<HTMLButtonElement>(".kitchen-step.is-next")!;
    expect(next).not.toBeNull();
    await click(next);
    expect(onTransitionItem).toHaveBeenCalledWith(data.tickets[0], data.tickets[0].items[0], "Ready");
  });

  it("shows the real table number instead of the order id when the order has a table", async () => {
    const withTable: KitchenData = {
      ...data,
      tickets: [{ ...data.tickets[0], tableId: "table-1", tableNumber: "7" }],
    };
    await render(<KitchenOperationsWorkspace {...baseProps({ data: withTable })} />);
    expect(document.body.textContent).toContain("Masa");
    expect(document.body.textContent).toContain("7");
    expect(document.querySelector(".kitchen-order__label")!.textContent).toBe("Masa");
  });

  it("falls back to the truncated order id for a table-less (takeaway/bar) order", async () => {
    await render(<KitchenOperationsWorkspace {...baseProps()} />);
    // The shared fixture's ticket has tableId/tableNumber: null.
    expect(document.querySelector(".kitchen-order__label")!.textContent).toBe("Sipariş");
    expect(document.querySelector(".kitchen-order__id")!.textContent).toBe("order-1");
  });

  it("shows the undo affordance right after a transition, even for a kitchen.advance-only (Mutfak Personeli) session", async () => {
    const onUndoItem = vi.fn();
    const fresh: KitchenData = {
      ...data,
      tickets: [{ ...data.tickets[0], items: [{ ...data.tickets[0].items[0], updatedAt: new Date().toISOString() }] }],
    };
    await render(<KitchenOperationsWorkspace {...baseProps({ data: fresh, canAdvance: true, canOperate: false, onUndoItem })} />);
    const undo = document.querySelector<HTMLButtonElement>(".kitchen-undo-btn");
    expect(undo).not.toBeNull();
    expect(undo!.textContent).toContain("Geri Al");
    await click(undo!);
    expect(onUndoItem).toHaveBeenCalledWith(fresh.tickets[0], fresh.tickets[0].items[0]);
  });

  it("hides the undo affordance once the window has passed", async () => {
    const old: KitchenData = {
      ...data,
      tickets: [{ ...data.tickets[0], items: [{ ...data.tickets[0].items[0], updatedAt: new Date(Date.now() - 20_000).toISOString() }] }],
    };
    await render(<KitchenOperationsWorkspace {...baseProps({ data: old, onUndoItem: vi.fn() })} />);
    expect(document.querySelector(".kitchen-undo-btn")).toBeNull();
  });

  it("shows a badge when kitchen.live_sync_enabled is off, and hides it when on", async () => {
    await render(<KitchenOperationsWorkspace {...baseProps({ data: { ...data, liveSyncEnabled: false } })} />);
    expect(document.querySelector(".kitchen-live-sync-badge")).not.toBeNull();
    expect(document.body.textContent).toContain("Canlı senkron kapalı");

    await render(<KitchenOperationsWorkspace {...baseProps({ data: { ...data, liveSyncEnabled: true } })} />);
    expect(document.querySelector(".kitchen-live-sync-badge")).toBeNull();
  });

  it("only a session with orders.send (canOperate) can open the cancel/sorun-bildir prompt", async () => {
    const onTransitionTicket = vi.fn();
    await render(<KitchenOperationsWorkspace {...baseProps({ canAdvance: true, canOperate: false, onTransitionTicket })} />);
    expect(document.querySelector(".kitchen-flag-btn")).toBeNull();
    // Advancing must still work for a kitchen.advance-only session.
    expect(document.querySelector(".kitchen-step.is-next")).not.toBeNull();
  });

  it("shows the age-restriction badge and its id-check hint", async () => {
    const withAgeRestriction: KitchenData = {
      ...data,
      tickets: [{ ...data.tickets[0], items: [{ ...data.tickets[0].items[0], isAgeRestricted: true }] }],
    };
    await render(<KitchenOperationsWorkspace {...baseProps({ data: withAgeRestriction })} />);
    const badge = document.querySelector(".kitchen-age-badge");
    expect(badge).not.toBeNull();
    expect(badge!.getAttribute("title")).toContain("kimlik kontrolü");
  });

  it("requires a reason before cancelling ('sorun bildir / iptal')", async () => {
    const onTransitionTicket = vi.fn();
    await render(<KitchenOperationsWorkspace {...baseProps({ onTransitionTicket })} />);
    await click(document.querySelector(".kitchen-flag-btn")!);
    const dialog = document.querySelector('[role="dialog"]')!;
    await click([...dialog.querySelectorAll("button")].find((button) => button.textContent?.includes("İptal et"))!);
    expect(dialog.textContent).toContain("Sorun/iptal gerekçesi gerekli.");
    const input = dialog.querySelector<HTMLInputElement>("input")!;
    await fill(input, "Malzeme bitti");
    await click([...dialog.querySelectorAll("button")].find((button) => button.textContent?.includes("İptal et"))!);
    expect(onTransitionTicket).toHaveBeenCalledWith(data.tickets[0], "Cancelled", "Malzeme bitti");
  });

  it("toggles dense mode on and off", async () => {
    await render(<KitchenOperationsWorkspace {...baseProps()} />);
    const workspace = document.querySelector(".kitchen-workspace")!;
    expect(workspace.className).not.toContain("is-dense");
    await click([...document.querySelectorAll(".kitchen-density button")].find((button) => button.textContent?.includes("Yoğun mod"))!);
    expect(workspace.className).toContain("is-dense");
  });

  // V1-KIT-013/V1-KDS-006: proves the threshold is actually read from
  // data.denseModeThreshold, not a hardcoded 9 — the shared fixture only
  // ever has 1 open item, so a hardcoded-9 implementation would also
  // leave this in sparse mode and the test would pass for the wrong
  // reason. Setting the threshold down to 1 (at or below the fixture's
  // real open-item count) must flip it to dense automatically.
  it("auto-switches to dense mode using the deployment's own threshold, not a hardcoded one", async () => {
    await render(<KitchenOperationsWorkspace {...baseProps({ data: { ...data, denseModeThreshold: 1 } })} />);
    expect(document.querySelector(".kitchen-workspace")!.className).toContain("is-dense");
  });

  it("stays in sparse mode when the open-item count is below the deployment's threshold", async () => {
    await render(<KitchenOperationsWorkspace {...baseProps({ data: { ...data, denseModeThreshold: 5 } })} />);
    expect(document.querySelector(".kitchen-workspace")!.className).not.toContain("is-dense");
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
    const note = document.querySelector(".kitchen-item-row__detail--note");
    expect(note).not.toBeNull();
    expect(note!.textContent).toContain("az tuz");
  });

  it("escalates the order's timer colour past the station preparation threshold", async () => {
    await render(<KitchenOperationsWorkspace {...baseProps()} />);
    const timer = document.querySelector(".kitchen-order__timer");
    expect(timer).not.toBeNull();
    expect(timer!.className).toContain("kitchen-order__timer--crit");
  });

  it("keeps the detailed health panel out of the operator view but shows the top-bar dot", async () => {
    await render(<KitchenOperationsWorkspace {...baseProps({ canManageReprints: false })} />);
    expect(document.querySelector(".kitchen-workspace__admin-health")).toBeNull();
    expect(document.body.textContent).not.toContain("Sağlık ve yedek");
    const dot = document.querySelector(".kitchen-workspace__header-actions .kitchen-health-dot");
    expect(dot).not.toBeNull();
    expect(dot!.getAttribute("aria-label")).toContain("Sistem durumu");
  });

  it("shows the Ürün Tükendi Bildir button locked for a kitchen-staff session and enabled for the chef", async () => {
    const onSuspendProductAvailability = vi.fn();
    await render(<KitchenOperationsWorkspace {...baseProps({ canSuspendAvailability: false, onSuspendProductAvailability })} />);
    const button = [...document.querySelectorAll("button")].find((candidate) => candidate.textContent?.includes("Ürün Tükendi Bildir"))!;
    expect(button).not.toBeNull();
    expect(button.hasAttribute("disabled")).toBe(true);
  });

  it("hides the Ürün Tükendi Bildir button entirely when the session has no cashier client at all", async () => {
    await render(<KitchenOperationsWorkspace {...baseProps({ onSuspendProductAvailability: undefined })} />);
    expect([...document.querySelectorAll("button")].some((candidate) => candidate.textContent?.includes("Ürün Tükendi Bildir"))).toBe(false);
  });

  it("86s a product picked from the open-ticket list and reports the manager notification when it was plan-conflicting", async () => {
    const onSuspendProductAvailability = vi.fn().mockResolvedValue({ productId: "product-1", isAvailable: false, planConflict: true });
    await render(<KitchenOperationsWorkspace {...baseProps({ onSuspendProductAvailability })} />);
    await click([...document.querySelectorAll("button")].find((candidate) => candidate.textContent?.includes("Ürün Tükendi Bildir"))!);
    const dialog = document.querySelector('[role="dialog"]')!;
    const submit = () => click([...dialog.querySelectorAll("button")].find((candidate) => candidate.textContent?.includes("Tükendi olarak işaretle"))!);

    // No product selected yet - a client-side guard, not a wasted round trip.
    await submit();
    expect(dialog.textContent).toContain("Bir ürün seçin.");
    expect(onSuspendProductAvailability).not.toHaveBeenCalled();

    const select = dialog.querySelector<HTMLSelectElement>("select")!;
    await act(async () => {
      const setter = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, "value")!.set!;
      setter.call(select, "product-1");
      select.dispatchEvent(new Event("change", { bubbles: true }));
    });
    await submit();

    expect(onSuspendProductAvailability).toHaveBeenCalledWith("product-1");
    expect(document.body.textContent).toContain("Mercimek çorbası tükendi olarak işaretlendi");
    expect(document.body.textContent).toContain("yöneticiye bildirim kaydı düşüldü");
  });

  it("86s a product without a manager notification when the suspend was not plan-conflicting", async () => {
    const onSuspendProductAvailability = vi.fn().mockResolvedValue({ productId: "product-1", isAvailable: false, planConflict: false });
    await render(<KitchenOperationsWorkspace {...baseProps({ onSuspendProductAvailability })} />);
    await click([...document.querySelectorAll("button")].find((candidate) => candidate.textContent?.includes("Ürün Tükendi Bildir"))!);
    const dialog = document.querySelector('[role="dialog"]')!;
    const select = dialog.querySelector<HTMLSelectElement>("select")!;
    await act(async () => {
      const setter = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, "value")!.set!;
      setter.call(select, "product-1");
      select.dispatchEvent(new Event("change", { bubbles: true }));
    });
    await click([...dialog.querySelectorAll("button")].find((candidate) => candidate.textContent?.includes("Tükendi olarak işaretle"))!);

    expect(document.body.textContent).toContain("Mercimek çorbası tükendi olarak işaretlendi.");
    expect(document.body.textContent).not.toContain("yöneticiye bildirim kaydı düşüldü");
  });

  it("has no critical or serious axe violations", async () => {
    await render(<KitchenOperationsWorkspace {...baseProps()} />);
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
