// @vitest-environment jsdom

import { act, type ComponentProps, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { TableWorkspace, type TableRecord, type TableZone } from "./index";
import { TableManagementApiError } from "./tableApi";

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
    activeReservationId: null,
    reservationRowVersion: null,
    // V1-TBL-009: well outside RECENTLY_VACATED_MINUTES so unrelated
    // assertions in this file don't unexpectedly see the "just vacated"
    // badge — tests that need it set their own recent value.
    statusChangedAt: new Date(Date.now() - 60 * 60_000).toISOString(),
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
    // V1-RMD-114: 30 minutes falls in the "warn" band under DESIGN.md's
    // 20/45-minute heatmap thresholds (previously a hardcoded 75/120).
    occupiedSince: new Date(Date.now() - 30 * 60_000).toISOString(),
    allowedCommands: ["SetAvailable", "Transfer", "Merge"],
    activeReservationId: null,
    reservationRowVersion: null,
    statusChangedAt: new Date(Date.now() - 30 * 60_000).toISOString(),
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
  it.each(["izgara", "IZGARA", "ızgara"])("finds a table whose number has a capital I when the search is %s", async (query) => {
    await render(<TableWorkspace {...baseProps({ selectedTableId: null, tables: [{ ...tables[0], tableNumber: "IZGARA-1" }, tables[1]] })} />);
    await fillInput(document.querySelector<HTMLInputElement>('input[aria-label="Masa ara"]')!, query);
    const cards = [...document.querySelectorAll(".table-card")].map((card) => card.querySelector("strong")?.textContent);
    expect(cards).toEqual(["IZGARA-1"]);
  });

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

  const busyTable = (id: string, overrides: Partial<TableRecord> = {}): TableRecord => ({
    ...tables[1], tableId: id, tableNumber: id.toUpperCase(), currentBillId: null, currentOrderId: "order-" + id, ...overrides,
  });

  it("names a table whose bill is open as waiting for payment, with icon and pattern as well as colour", async () => {
    await render(<TableWorkspace {...baseProps({ selectedTableId: null })} />);

    const card = [...document.querySelectorAll<HTMLElement>(".table-card")].find((element) => element.textContent?.includes("S-10"))!;
    expect(card.classList.contains("table-card--plan-pay")).toBe(true);
    expect(card.querySelector(".table-card__icon")?.textContent).toBe("₺");
    expect(card.querySelector(".table-status")?.textContent).toBe("Ödeme bekliyor");
    expect(card.querySelector(".table-card__select")?.getAttribute("aria-label")).toBe("S-10 masasını seç, Ödeme bekliyor");
  });

  it("marks a table whose check was sent to the till as asking for the bill, only when the till list says so", async () => {
    const asking = busyTable("s-11", { status: "Cleaning", currentOrderId: null });
    await render(<TableWorkspace {...baseProps({ selectedTableId: null, tables: [tables[0], asking], awaitingCheckTableIds: new Set(["s-11"]) })} />);
    const card = document.querySelector<HTMLElement>(".table-card--plan-ask")!;
    expect(card.textContent).toContain("Hesap istedi");
    expect(card.querySelector(".table-card__icon")?.textContent).toBe("⚠");

    await render(<TableWorkspace {...baseProps({ selectedTableId: null, tables: [tables[0], asking] })} />);
    expect(document.querySelector(".table-card--plan-ask")).toBeNull();
    expect(document.querySelector(".table-card--plan-clean")).not.toBeNull();
  });

  it("shows amount, age, waiter and party size on a busy card and no amount on an empty one", async () => {
    const busy = busyTable("s-12", { currentOrderTotal: 245.5, servingWaiterName: "Ayşe", partySize: 3 });
    await render(<TableWorkspace {...baseProps({ selectedTableId: null, tables: [tables[0], busy] })} />);

    const card = [...document.querySelectorAll<HTMLElement>(".table-card")].find((element) => element.textContent?.includes("S-12"))!;
    expect(card.querySelector(".table-card__amount")?.textContent).toBe("₺245,50");
    expect(card.querySelector(".table-card__meta")?.textContent).toBe("Ayşe · 3 kişi");
    expect(card.querySelector(".table-card__elapsed")?.textContent).toBe("30 dk");
    const empty = [...document.querySelectorAll<HTMLElement>(".table-card")].find((element) => element.textContent?.includes("S-09"))!;
    expect(empty.querySelector(".table-card__amount")).toBeNull();
    expect(empty.querySelector(".table-card__meta")).toBeNull();
  });

  it("lists the attention groups and selects the first table of a group on tap", async () => {
    const onSelectTable = vi.fn();
    const asking = busyTable("s-13", { status: "Cleaning", currentOrderId: null });
    await render(<TableWorkspace {...baseProps({ selectedTableId: null, onSelectTable, tables: [tables[0], tables[1], asking], awaitingCheckTableIds: new Set(["s-13"]) })} />);

    const chips = [...document.querySelectorAll<HTMLButtonElement>(".table-attention button")];
    expect(chips.map((chip) => chip.textContent)).toEqual(["⚠ Hesap isteyen · S-13", "₺ Ödeme bekleyen · S-10"]);
    await click(chips[0]);
    expect(onSelectTable).toHaveBeenCalledWith("s-13");
  });

  it("shows the attention strip only when a group has tables", async () => {
    await render(<TableWorkspace {...baseProps({ selectedTableId: null, tables: [tables[0]] })} />);
    expect(document.querySelector(".table-attention")).toBeNull();
  });

  it("filters by zone with tabs instead of a dropdown", async () => {
    const terrace = { ...tables[0], tableId: "table-t1", tableNumber: "T-01", zoneId: "zone-terrace" };
    await render(<TableWorkspace {...baseProps({ selectedTableId: null, tables: [...tables, terrace] })} />);

    const tabs = [...document.querySelectorAll<HTMLButtonElement>('.table-zone-tabs button')];
    expect(tabs.map((tab) => tab.textContent)).toEqual(["Tüm bölgeler", "Salon", "Teras"]);
    expect(tabs[0].getAttribute("aria-pressed")).toBe("true");
    await click(tabs[2]);
    expect(tabs[2].getAttribute("aria-pressed")).toBe("true");
    expect([...document.querySelectorAll(".table-card")].map((card) => card.querySelector("strong")?.textContent)).toEqual(["T-01"]);
  });

  it("warns that asking tables may be missing when the till list could not be read", async () => {
    await render(<TableWorkspace {...baseProps({ selectedTableId: null, tables: [tables[0]], awaitingChecksUnavailable: true })} />);
    expect(document.querySelector(".table-attention__unavailable")?.textContent).toContain("alınamadı");
  });

  it("explains every status icon in the legend and names the plan status in the details panel", async () => {
    await render(<TableWorkspace {...baseProps({ selectedTableId: "table-10" })} />);
    const legend = document.querySelector(".table-workspace__legend")?.textContent ?? "";
    for (const label of ["Müsait", "Dolu", "Hesap istedi", "Ödeme bekliyor", "Rezerve", "Toplanıyor", "Servis dışı"]) expect(legend).toContain(label);
    expect(document.querySelector(".table-details__status")?.textContent).toBe("Ödeme bekliyor");
  });

  it("shows the capacity only on a free table and puts the amount in the card's accessible name", async () => {
    const busy = busyTable("s-14", { currentOrderTotal: 80 });
    await render(<TableWorkspace {...baseProps({ selectedTableId: null, tables: [tables[0], busy] })} />);
    const cards = [...document.querySelectorAll<HTMLElement>(".table-card")];
    expect(cards[0].querySelector(".table-card__capacity")).not.toBeNull();
    expect(cards[1].querySelector(".table-card__capacity")).toBeNull();
    expect(cards[1].querySelector(".table-card__select")?.getAttribute("aria-label")).toBe("S-14 masasını seç, Dolu, ₺80,00");
  });

  it("opens on the table grid even when a floor plan exists", async () => {
    await render(<TableWorkspace {...baseProps({ selectedTableId: null, floorPlan: { zoneId: "zone-salon", rowVersion: 1, tables: [] } as never })} />);
    expect(document.querySelectorAll(".table-card")).toHaveLength(2);
    expect(document.querySelector(".table-workspace__view-toggle button[aria-pressed='true']")?.textContent).toBe("Liste");
  });

  it("renders a dense zone-filtered map, authoritative context and capability actions", async () => {
    const props = baseProps({ selectedTableId: null });
    await render(<TableWorkspace {...props} />);

    expect(document.querySelector("h2")?.textContent).toBe("Masa düzeni");
    expect(document.body.textContent).toContain("S-09");
    expect(document.body.textContent).toContain("S-10");
    expect(document.body.textContent).toContain("30 dk");
    expect(document.body.textContent).not.toContain("Satır sürümü");
    expect(document.body.textContent).not.toContain("order-12345678");
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
    expect(document.querySelector('[role="dialog"]')?.textContent).toContain("S-09 · Müsait");
    expect(document.querySelector('[role="dialog"]')?.textContent).not.toContain("v3");
  });

  it("keeps manager drafts on validation and submits zone/table creation", async () => {
    const onCreateZone = vi.fn().mockResolvedValue(undefined);
    const onCreateTable = vi.fn().mockResolvedValue(undefined);
    await render(<TableWorkspace {...baseProps({ onCreateZone, onCreateTable })} />);

    await click([...document.querySelectorAll("button")].find((button) => button.textContent === "+ Bölge ekle")!);
    const dialog = document.querySelector('[role="dialog"]')!;
    const zoneSubmit = [...dialog.querySelectorAll("button")].find((button) => button.textContent === "Bölge oluştur")!;
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
    // Table actions go through tableApi.ts, so a real conflict reaches this
    // component as a TableManagementApiError carrying the server's own
    // CONCURRENT_MODIFICATION code (this used to be faked with the shared
    // ApiError and matched on the message text, which never happens for real).
    const onAction = vi.fn().mockRejectedValue(new TableManagementApiError(409, "CONCURRENT_MODIFICATION", "Kayıt başka bir işlem tarafından değiştirildi."));
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
    await click(confirm);
    expect(onAction).toHaveBeenCalledWith({ idempotencyKey: expect.stringMatching(/.+/), table: tables[1], action: "Transfer", reason: "Misafir masa değiştirdi", targetTableId: "table-09", targetTableVersion: 3, participantTableIds: undefined, participantTableVersions: undefined });
    // Pressing "Onayla" again repeats the same command, so it carries the same key.
    await click(confirm);
    expect(onAction).toHaveBeenCalledTimes(2);
    expect(onAction.mock.calls[1][0].idempotencyKey).toBe(onAction.mock.calls[0][0].idempotencyKey);
    // The conflict is reported inside the dialog, which stays open for a second look.
    expect(document.querySelector('[role="dialog"]')?.textContent).toContain("Masa başka biri tarafından değiştirildi");
  });

  it("offers a prepared reason and asks for free text only for 'Diğer'", async () => {
    const onAction = vi.fn().mockResolvedValue(undefined);
    await render(<TableWorkspace {...baseProps({ selectedTableId: "table-10", onAction })} />);
    await click([...document.querySelectorAll(".table-details button")].find((button) => button.textContent === "Masa değiştir")!);
    const dialog = document.querySelector('[role="dialog"]')!;
    const [targetSelect, reasonSelect] = [...dialog.querySelectorAll("select")] as HTMLSelectElement[];
    await act(async () => { targetSelect.value = "table-09"; targetSelect.dispatchEvent(new Event("change", { bubbles: true })); });
    expect(dialog.querySelector("input")).toBeNull();

    await act(async () => { reasonSelect.value = "__other"; reasonSelect.dispatchEvent(new Event("change", { bubbles: true })); });
    const confirm = [...dialog.querySelectorAll("button")].find((button) => button.textContent === "Onayla")!;
    await click(confirm);
    expect(dialog.textContent).toContain("Bu işlem için açıklama gerekli.");
    await fillInput(dialog.querySelector<HTMLInputElement>("input")!, "Müşteri isteği");
    await click(confirm);
    expect(onAction).toHaveBeenCalledWith(expect.objectContaining({ action: "Transfer", reason: "Müşteri isteği" }));
  });

  it("offers only free tables as transfer targets and only free or busy ones for a merge", async () => {
    const mixed: TableRecord[] = [
      tables[1],
      { ...tables[0], tableId: "t-free", tableNumber: "F-1", status: "Available" },
      { ...tables[0], tableId: "t-res", tableNumber: "R-1", status: "Reserved" },
      { ...tables[0], tableId: "t-busy", tableNumber: "B-1", status: "Occupied" },
      { ...tables[0], tableId: "t-clean", tableNumber: "C-1", status: "Cleaning" },
      { ...tables[0], tableId: "t-off", tableNumber: "X-1", status: "OutOfService" },
      { ...tables[0], tableId: "t-inactive", tableNumber: "I-1", status: "Available", active: false },
    ];
    await render(<TableWorkspace {...baseProps({ tables: mixed, selectedTableId: "table-10" })} />);
    await click([...document.querySelectorAll(".table-details button")].find((button) => button.textContent === "Masa değiştir")!);
    const transferOptions = [...document.querySelectorAll('[role="dialog"] select')[0].querySelectorAll("option")].map((option) => option.textContent);
    expect(transferOptions).toEqual(["Hedef seçin", "F-1"]);

    await click([...document.querySelectorAll('[role="dialog"] button')].find((button) => button.textContent === "Vazgeç")!);
    await click([...document.querySelectorAll(".table-details button")].find((button) => button.textContent === "Masaları birleştir")!);
    const mergeLabels = [...document.querySelectorAll('[role="dialog"] fieldset label')].map((label) => label.textContent?.trim());
    expect(mergeLabels).toEqual(["F-1 · Müsait", "B-1 · Dolu"]);
  });

  it("says so when there is no free table to transfer to", async () => {
    await render(<TableWorkspace {...baseProps({ tables: [tables[1]], selectedTableId: "table-10" })} />);
    await click([...document.querySelectorAll(".table-details button")].find((button) => button.textContent === "Masa değiştir")!);
    expect(document.querySelector('[role="dialog"]')?.textContent).toContain("Şu anda boş masa yok");
  });

  it("shows why an action is dimmed as text, not only as a tooltip", async () => {
    const reserved: TableRecord = { ...tables[0], status: "Reserved", allowedCommands: ["ClaimReservation", "CancelReservation"], activeReservationId: null };
    await render(<TableWorkspace {...baseProps({ tables: [reserved], selectedTableId: "table-09" })} />);
    expect(document.querySelector(".table-details__disabled-reason")?.textContent).toContain("aktif rezervasyon bulunamadı");
  });

  it("raises a banner for a failed background refresh and keeps the tables on screen", async () => {
    await render(<TableWorkspace {...baseProps({ refreshError: "Sunucuya ulaşılamadı." })} />);
    expect(document.querySelector(".table-workspace__refresh-error")?.textContent).toContain("son bilinen durum");
    expect(document.querySelectorAll(".table-card")).toHaveLength(2);
  });

  it("shows the backend's own message for an unsettled-payment rejection, not the generic retry text or the stale-conflict text", async () => {
    const message = "Bu masanın hesabında çözülmemiş bir ödeme var. Ödeme mutabakatı tamamlanmadan masa taşınamaz veya birleştirilemez.";
    const onAction = vi.fn().mockRejectedValue(new TableManagementApiError(409, "PAYMENT_UNSETTLED", message));
    await render(<TableWorkspace {...baseProps({ selectedTableId: "table-10", onAction })} />);
    await click([...document.querySelectorAll(".table-details button")].find((button) => button.textContent === "Masa değiştir")!);
    const dialog = document.querySelector('[role="dialog"]')!;
    const confirm = [...dialog.querySelectorAll("button")].find((button) => button.textContent === "Onayla")!;
    const select = dialog.querySelector("select") as HTMLSelectElement;
    await act(async () => {
      select.value = "table-09";
      select.dispatchEvent(new Event("change", { bubbles: true }));
    });
    await click(confirm);
    expect(dialog.textContent).toContain(message);
    expect(document.body.textContent).not.toContain("Tekrar deneyin");
    expect(document.body.textContent).not.toContain("başka biri tarafından değiştirildi");
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

  it("escalates the occupancy elapsed colour past the warning threshold", async () => {
    await render(<TableWorkspace {...baseProps()} />);
    const warned = document.querySelector(".table-card__elapsed--warn");
    expect(warned).not.toBeNull();
    expect(warned!.textContent).toContain("dk");
  });

  it("flags a table past the critical threshold in red", async () => {
    // DESIGN.md's 45-minute critical boundary.
    const stale = tables.map((table) => table.status === "Occupied"
      ? { ...table, occupiedSince: new Date(Date.now() - 95 * 60_000).toISOString() }
      : table);
    await render(<TableWorkspace {...baseProps({ tables: stale })} />);
    const critical = document.querySelector(".table-card__elapsed--crit");
    expect(critical).not.toBeNull();
    expect(critical!.textContent).toContain("sa");
  });

  it("keeps the elapsed colour neutral for a freshly occupied table", async () => {
    const fresh = tables.map((table) => table.status === "Occupied"
      ? { ...table, occupiedSince: new Date(Date.now() - 5 * 60_000).toISOString() }
      : table);
    await render(<TableWorkspace {...baseProps({ tables: fresh })} />);
    expect(document.querySelector(".table-card__elapsed--warn")).toBeNull();
    expect(document.querySelector(".table-card__elapsed--crit")).toBeNull();
  });

  // V1-TBL-009: the passive "just vacated" hint — no extra staff click, it
  // only depends on how recently the table's own status last changed.
  it("shows a passive hint on an Available table that changed status a moment ago", async () => {
    const justVacated = tables.map((table) => table.status === "Available"
      ? { ...table, statusChangedAt: new Date(Date.now() - 2 * 60_000).toISOString() }
      : table);
    await render(<TableWorkspace {...baseProps({ tables: justVacated })} />);
    expect(document.querySelector(".table-card__recently-vacated")?.textContent).toBe("Az önce boşaldı");
  });

  it("hides the hint once the recently-vacated window has passed", async () => {
    const longVacant = tables.map((table) => table.status === "Available"
      ? { ...table, statusChangedAt: new Date(Date.now() - 30 * 60_000).toISOString() }
      : table);
    await render(<TableWorkspace {...baseProps({ tables: longVacant })} />);
    expect(document.querySelector(".table-card__recently-vacated")).toBeNull();
  });

  it("never shows the hint on an Occupied table regardless of statusChangedAt", async () => {
    const recentlyOccupied = tables.map((table) => table.status === "Occupied"
      ? { ...table, statusChangedAt: new Date(Date.now() - 1 * 60_000).toISOString() }
      : table);
    await render(<TableWorkspace {...baseProps({ tables: recentlyOccupied })} />);
    expect(document.querySelector(".table-card__recently-vacated")).toBeNull();
  });
  const buttonByText = (scope: ParentNode, text: string) => [...scope.querySelectorAll("button")].find((button) => button.textContent === text)!;

  it("offers table and zone editing only to a manager session with the handlers", async () => {
    await render(<TableWorkspace {...baseProps({ canManage: false, onUpdateTable: vi.fn(), onUpdateZone: vi.fn() })} />);
    expect([...document.querySelectorAll("button")].map((button) => button.textContent)).not.toContain("Masayı düzenle");
    expect([...document.querySelectorAll("button")].map((button) => button.textContent)).not.toContain("Bölgeleri yönet");
  });

  it("edits the selected table with its current values and the table as sent for the row version", async () => {
    const onUpdateTable = vi.fn().mockResolvedValue(undefined);
    await render(<TableWorkspace {...baseProps({ onUpdateTable })} />);
    await click(buttonByText(document, "Masayı düzenle"));
    const dialog = document.querySelector('[role="dialog"]')!;
    const inputs = dialog.querySelectorAll<HTMLInputElement>("input");
    expect(inputs[0].value).toBe("S-09");
    await fillInput(inputs[0], "");
    await click(buttonByText(dialog, "Masayı kaydet"));
    expect(dialog.textContent).toContain("Masa numarası gerekli.");
    expect(onUpdateTable).not.toHaveBeenCalled();
    await fillInput(inputs[0], "S-90");
    await click(buttonByText(dialog, "Masayı kaydet"));
    expect(onUpdateTable).toHaveBeenCalledWith(tables[0], { tableNumber: "S-90", zoneId: "zone-salon", capacity: 4, active: true });
    expect(document.querySelector('[role="dialog"]')).toBeNull();
  });

  it("keeps the table edit open and tells the manager to refresh after a concurrency conflict", async () => {
    const onUpdateTable = vi.fn().mockRejectedValue(new TableManagementApiError(409, "CONCURRENT_MODIFICATION", "Satır sürümü eski."));
    await render(<TableWorkspace {...baseProps({ onUpdateTable })} />);
    await click(buttonByText(document, "Masayı düzenle"));
    const dialog = document.querySelector('[role="dialog"]')!;
    await click(buttonByText(dialog, "Masayı kaydet"));
    expect(dialog.textContent).toContain("Kayıt başka biri tarafından değiştirildi");
    expect(document.querySelector('[role="dialog"]')).not.toBeNull();
  });

  it("renames a zone keeping its order and active flag", async () => {
    const onUpdateZone = vi.fn().mockResolvedValue(undefined);
    await render(<TableWorkspace {...baseProps({ onUpdateZone })} />);
    await click(buttonByText(document, "Bölgeleri yönet"));
    const dialog = document.querySelector('[role="dialog"]')!;
    await click(buttonByText(dialog, "Düzenle"));
    await fillInput(dialog.querySelector<HTMLInputElement>('form[aria-label="Bölgeyi düzenle"] input')!, "Salon 2");
    await click(buttonByText(dialog, "Bölgeyi kaydet"));
    expect(onUpdateZone).toHaveBeenCalledWith(zones[0], { name: "Salon 2", sortOrder: 1, active: true });
  });

  it("deletes a zone only after confirmation and shows the server's refusal", async () => {
    const onDeleteZone = vi.fn().mockRejectedValueOnce(new TableManagementApiError(409, "ZONE_HAS_TABLES", "Bölgede masa var.")).mockResolvedValue(undefined);
    await render(<TableWorkspace {...baseProps({ onDeleteZone, onUpdateZone: vi.fn() })} />);
    await click(buttonByText(document, "Bölgeleri yönet"));
    const dialog = document.querySelector('[role="dialog"]')!;
    await click(buttonByText(dialog, "Sil"));
    expect(onDeleteZone).not.toHaveBeenCalled();
    await click(buttonByText(dialog, "Evet, bölgeyi sil"));
    expect(onDeleteZone).toHaveBeenCalledWith(zones[0]);
    expect(dialog.textContent).toContain("Bölgede masa var.");
    await click(buttonByText(dialog, "Evet, bölgeyi sil"));
    expect(onDeleteZone).toHaveBeenCalledTimes(2);
  });
});
