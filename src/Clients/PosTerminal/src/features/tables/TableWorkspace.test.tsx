// @vitest-environment jsdom

import { act, type ComponentProps, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { TableWorkspace, type TableRecord, type TableZone } from "./index";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const zones: TableZone[] = [
  { zoneId: "zone-salon", code: "SALON", name: "Salon", sortOrder: 1, active: true, rowVersion: 1 },
  { zoneId: "zone-terrace", code: "TERAS", name: "Teras", sortOrder: 2, active: true, rowVersion: 1 },
];

const tables: TableRecord[] = [
  {
    tableId: "table-09",
    tableNumber: "S-09",
    zoneId: "zone-salon",
    capacity: 4,
    active: true,
    status: "Available",
    currentOrderId: null,
    currentBillId: null,
    rowVersion: 3,
    allowedCommands: ["Update", "SetOccupied", "Reserve"],
  },
  {
    tableId: "table-10",
    tableNumber: "S-10",
    zoneId: "zone-salon",
    capacity: 2,
    active: true,
    status: "Occupied",
    currentOrderId: "order-12345678",
    currentBillId: "bill-12345678",
    rowVersion: 9,
    occupiedSince: new Date(Date.now() - 95 * 60_000).toISOString(),
    allowedCommands: ["SetAvailable", "Transfer", "Merge"],
  },
];

function baseProps(overrides: Partial<ComponentProps<typeof TableWorkspace>> = {}) {
  return {
    state: "ready" as const,
    zones,
    tables,
    canManage: true,
    selectedTableId: "table-09",
    onSelectTable: vi.fn(),
    onRefresh: vi.fn(),
    onCreateZone: vi.fn(),
    onCreateTable: vi.fn(),
    onAction: vi.fn(),
    ...overrides,
  };
}

describe("table workspace", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.title = "ALKAROS masa düzeni";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  async function click(element: Element) {
    await act(async () => (element as HTMLElement).click());
  }

  async function fillInput(input: HTMLInputElement, value: string) {
    await act(async () => {
      const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;
      setter.call(input, value);
      input.dispatchEvent(new Event("input", { bubbles: true }));
      input.dispatchEvent(new Event("change", { bubbles: true }));
    });
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.restoreAllMocks();
  });

  it("renders a dense zone-filtered map, authoritative context and capability actions", async () => {
    const props = baseProps({ selectedTableId: null });
    await render(<TableWorkspace {...props} />);

    expect(document.querySelector("h2")?.textContent).toBe("Masa düzeni");
    expect(document.body.textContent).toContain("S-09");
    expect(document.body.textContent).toContain("S-10");
    expect(document.body.textContent).toContain("Sipariş #order-12");
    expect(document.body.textContent).toContain("1 sa 35 dk");
    expect(document.body.textContent).toContain("Satır sürümü");
    expect(document.querySelectorAll(".table-card")).toHaveLength(2);
    expect(document.querySelector('.table-workspace__filters[role="group"][aria-label="Masa filtreleri"]')).not.toBeNull();
    expect(document.querySelector('.table-workspace__stats[role="group"][aria-label="Masa özeti"]')).not.toBeNull();
    expect(document.querySelector(".table-details")?.tagName).toBe("SECTION");

    const occupiedFilter = [...document.querySelectorAll(".table-workspace__status-filter button")].find((button) => button.textContent === "Dolu")!;
    await click(occupiedFilter);
    expect(document.querySelectorAll(".table-card")).toHaveLength(1);
    expect(document.querySelector(".table-card")?.textContent).toContain("S-10");

    await click(document.querySelector(".table-card__select")!);
    const actionButton = [...document.querySelectorAll(".table-details button")].find((button) => button.textContent === "Masa değiştir")!;
    expect(actionButton).toBeTruthy();
    await click(actionButton);
    expect(document.querySelector('[role="dialog"]')?.textContent).toContain("Hedef masa");
  });

  it("exposes table selection and quick action as independent keyboard controls", async () => {
    const onSelectTable = vi.fn();
    await render(<TableWorkspace {...baseProps({ selectedTableId: "table-10", onSelectTable })} />);

    const card = [...document.querySelectorAll<HTMLElement>(".table-card")].find((element) => element.textContent?.includes("S-09"))!;
    const selectButton = card.querySelector<HTMLButtonElement>(".table-card__select")!;
    const quickAction = card.querySelector<HTMLButtonElement>(".table-card__quick-action")!;

    expect(card.tagName).toBe("ARTICLE");
    expect(card.querySelector("button button")).toBeNull();
    expect(selectButton.getAttribute("aria-label")).toBe("S-09 masasını seç, Müsait");
    expect(quickAction.getAttribute("aria-label")).toBe("S-09: Masayı aç");

    selectButton.focus();
    expect(document.activeElement).toBe(selectButton);
    await click(selectButton);
    expect(onSelectTable).toHaveBeenCalledTimes(1);
    expect(onSelectTable).toHaveBeenCalledWith("table-09");

    onSelectTable.mockClear();
    quickAction.focus();
    expect(document.activeElement).toBe(quickAction);
    await click(quickAction);
    expect(onSelectTable).not.toHaveBeenCalled();
    expect(document.querySelector('[role="dialog"]')?.textContent).toContain("Masayı aç");
    expect(document.querySelector('[role="dialog"]')?.textContent).toContain("S-09 · Müsait · v3");
  });

  it("keeps manager drafts on validation and submits zone/table creation", async () => {
    const onCreateZone = vi.fn().mockResolvedValue(undefined);
    const onCreateTable = vi.fn().mockResolvedValue(undefined);
    await render(<TableWorkspace {...baseProps({ onCreateZone, onCreateTable })} />);

    await click([...document.querySelectorAll("button")].find((button) => button.textContent === "+ Zone ekle")!);
    const dialog = document.querySelector('[role="dialog"]')!;
    const zoneSubmit = [...dialog.querySelectorAll("button")].find((button) => button.textContent === "Zone oluştur")!;
    await click(zoneSubmit);
    expect(dialog.textContent).toContain("Kod gerekli.");
    const inputs = dialog.querySelectorAll<HTMLInputElement>("input");
    await fillInput(inputs[0], "BAR");
    await fillInput(inputs[1], "Bar");
    await click(zoneSubmit);
    expect(onCreateZone).toHaveBeenCalledWith({ code: "BAR", name: "Bar", sortOrder: 0 });
    expect(document.querySelector('[role="dialog"]')).toBeNull();

    await click([...document.querySelectorAll("button")].find((button) => button.textContent === "+ Masa ekle")!);
    const tableDialog = document.querySelector('[role="dialog"]')!;
    const tableInputs = tableDialog.querySelectorAll<HTMLInputElement>("input");
    await fillInput(tableInputs[0], "S-11");
    await fillInput(tableInputs[1], "3");
    await click([...tableDialog.querySelectorAll("button")].find((button) => button.textContent === "Masa oluştur")!);
    expect(onCreateTable).toHaveBeenCalledWith({ tableNumber: "S-11", zoneId: null, capacity: 3 });
  });

  it("requires transfer target and reason, then preserves context after a stale conflict", async () => {
    const onAction = vi.fn().mockRejectedValue(new Error("409 concurrent modification"));
    await render(<TableWorkspace {...baseProps({ selectedTableId: "table-10", onAction })} />);
    await click([...document.querySelectorAll(".table-details button")].find((button) => button.textContent === "Masa değiştir")!);
    const dialog = document.querySelector('[role="dialog"]')!;
    const confirm = [...dialog.querySelectorAll("button")].find((button) => button.textContent === "Onayla")!;
    await click(confirm);
    expect(dialog.textContent).toContain("Hedef masa seçin.");
    const select = dialog.querySelector("select") as HTMLSelectElement;
    await act(async () => {
      select.value = "table-09";
      select.dispatchEvent(new Event("change", { bubbles: true }));
    });
    const reasonInput = dialog.querySelector<HTMLInputElement>("input")!;
    await fillInput(reasonInput, "Servis yönlendirmesi");
    await click(confirm);
    expect(onAction).toHaveBeenCalledWith({ table: tables[1], action: "Transfer", reason: "Servis yönlendirmesi", targetTableId: "table-09", targetTableVersion: 3, participantTableIds: undefined, participantTableVersions: undefined });
    expect(document.body.textContent).toContain("Sipariş #order-12");
    expect(document.body.textContent).toContain("Masa güncellendi");
  });

  it.each([
    ["loading", "Masa düzeni yükleniyor"],
    ["offline", "Bağlantı yok"],
    ["unauthorized", "Oturum gerekli"],
    ["error", "Masa düzeni alınamadı"],
    ["stale", "Masa verisi güncel değil"],
  ] as const)("shows bounded %s state", async (state, title) => {
    await render(<TableWorkspace {...baseProps({ state, errorMessage: "API kapalı" })} />);
    expect(document.body.textContent).toContain(title);
    expect(document.querySelector(".table-workspace--state")).toBeTruthy();
  });

  it("has no critical or serious axe violations", async () => {
    await render(<TableWorkspace {...baseProps()} />);
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect([...report.violations, ...report.incomplete].filter((item) => item.impact === "critical" || item.impact === "serious")).toEqual([]);
    expect(report.violations.some((violation) => violation.id === "landmark-complementary-is-top-level")).toBe(false);
  });
});
