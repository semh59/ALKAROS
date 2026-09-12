// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../../api";
import { FloorPlanWorkspace, type FloorPlan, type TableRecord } from "./index";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const plan: FloorPlan = {
  zoneId: "zone-main",
  zoneCode: "MAIN",
  zoneName: "Ana Salon",
  canvasWidth: 1000,
  canvasHeight: 600,
  rowVersion: 4,
  tables: [
    {
      tableId: "table-a",
      tableNumber: "A-01",
      capacity: 1,
      active: true,
      status: "Occupied",
      currentOrderId: "order-12345678",
      currentBillId: "bill-12345678",
      tableRowVersion: 7,
      x: 100,
      y: 100,
      width: 160,
      height: 96,
      shape: "Rectangle",
      rotationDegrees: 0,
      layoutRowVersion: 2,
      activeReservationId: null,
      reservationPartySize: null,
      reservedAt: null,
      reservationExpiresAt: null,
      mergeGroupId: "merge-1",
      isMergePrimary: true,
      seats: [{ seatId: "seat-a", number: 1, label: "Pencere", x: 100, y: 148, rowVersion: 3 }],
      allowedCommands: ["Transfer", "Merge"],
    },
    {
      tableId: "table-b",
      tableNumber: "A-02",
      capacity: 0,
      active: true,
      status: "Available",
      currentOrderId: null,
      currentBillId: null,
      tableRowVersion: 2,
      x: 600,
      y: 320,
      width: 112,
      height: 112,
      shape: "Round",
      rotationDegrees: 0,
      layoutRowVersion: 1,
      activeReservationId: null,
      reservationPartySize: null,
      reservedAt: null,
      reservationExpiresAt: null,
      mergeGroupId: null,
      isMergePrimary: false,
      seats: [],
      allowedCommands: ["SetOccupied", "Reserve"],
    },
  ],
};

const tables: TableRecord[] = plan.tables.map((table) => ({
  tableId: table.tableId,
  tableNumber: table.tableNumber,
  zoneId: plan.zoneId,
  capacity: table.capacity,
  active: table.active,
  status: table.status,
  currentOrderId: table.currentOrderId,
  currentBillId: table.currentBillId,
  rowVersion: table.tableRowVersion,
  allowedCommands: table.allowedCommands,
  activeReservationId: table.activeReservationId,
  reservationRowVersion: null,
  statusChangedAt: new Date(Date.now() - 60 * 60_000).toISOString(),
}));

describe("floor plan workspace", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.title = "ALKAROS salon planı";
    document.body.innerHTML = '<main id="root"></main>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  async function click(element: Element) {
    await act(async () => (element as HTMLElement).click());
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.restoreAllMocks();
  });

  it("renders authoritative geometry, seats and operational context", async () => {
    const onOpenAction = vi.fn();
    await render(<FloorPlanWorkspace plan={plan} tables={tables} selectedTableId="table-a" canManage onSelectTable={vi.fn()} onOpenAction={onOpenAction} onSave={vi.fn()} lastUpdated="08:00" />);

    expect(document.querySelector(".floor-plan-canvas")?.getAttribute("style")).toContain("1000 / 600");
    expect(document.querySelectorAll(".floor-plan-table")).toHaveLength(2);
    expect(document.querySelector(".floor-plan-table--round")).not.toBeNull();
    expect(document.querySelector(".floor-plan-seat")?.textContent).toBe("1");
    expect(document.body.textContent).toContain("Birleşim lideri");
    expect(document.body.textContent).toContain("Hesap bill-123");
    expect(document.body.textContent).toContain("Sunucunun izin verdiği işlemler");
    await click([...document.querySelectorAll("button")].find((button) => button.textContent === "Masa değiştir")!);
    expect(onOpenAction).toHaveBeenCalledWith("Transfer", tables[0]);
  });

  it("moves by keyboard and sends every authoritative version in one save", async () => {
    const onSave = vi.fn().mockResolvedValue({ floorPlan: { ...plan, rowVersion: 5 }, warnings: [] });
    await render(<FloorPlanWorkspace plan={plan} tables={tables} selectedTableId="table-a" canManage onSelectTable={vi.fn()} onSave={onSave} />);
    await click([...document.querySelectorAll("button")].find((button) => button.textContent === "Planı düzenle")!);

    const table = document.querySelector<HTMLButtonElement>('.floor-plan-table[aria-label^="A-01"]')!;
    await act(async () => table.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowRight", bubbles: true })));
    await click([...document.querySelectorAll("button")].find((button) => button.textContent === "İncele ve kaydet")!);

    expect(onSave).toHaveBeenCalledTimes(1);
    expect(onSave.mock.calls[0][0]).toBe("zone-main");
    expect(onSave.mock.calls[0][1]).toMatchObject({
      expectedRowVersion: 4,
      canvasWidth: 1000,
      tables: [
        {
          tableId: "table-a",
          expectedTableRowVersion: 7,
          expectedLayoutRowVersion: 2,
          x: 108,
          seats: [{ seatId: "seat-a", expectedRowVersion: 3, x: 108 }],
        },
        { tableId: "table-b", expectedTableRowVersion: 2, expectedLayoutRowVersion: 1 },
      ],
    });
    expect(document.body.textContent).toContain("Salon planı atomik olarak kaydedildi");
  });

  it("keeps the edited draft and setup mode after a conflict", async () => {
    // V1-RMD-114: a real conflict always reaches this component as an
    // ApiError (produced by api.ts's own response-mapping); a plain Error
    // would now correctly be treated as an untrusted client-side/network
    // failure and not have its message surfaced.
    const onSave = vi.fn().mockRejectedValue(new ApiError(409, "CONCURRENCY_CONFLICT", "409 concurrent version"));
    await render(<FloorPlanWorkspace plan={plan} tables={tables} selectedTableId="table-a" canManage onSelectTable={vi.fn()} onSave={onSave} />);
    await click([...document.querySelectorAll("button")].find((button) => button.textContent === "Planı düzenle")!);
    const table = document.querySelector<HTMLButtonElement>('.floor-plan-table[aria-label^="A-01"]')!;
    await act(async () => table.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowDown", bubbles: true })));
    await click([...document.querySelectorAll("button")].find((button) => button.textContent === "İncele ve kaydet")!);

    expect(document.querySelector('[role="alert"]')?.textContent).toContain("Taslağınız korundu");
    expect(document.body.textContent).toContain("KURULUM MODU");
    expect(document.querySelectorAll<HTMLInputElement>(".floor-plan-setup-fields__grid input")[1].value).toBe("108");
  });

  it("has no critical or serious automated accessibility findings", async () => {
    await render(<FloorPlanWorkspace plan={plan} tables={tables} selectedTableId="table-a" canManage onSelectTable={vi.fn()} onSave={vi.fn()} />);
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect([...report.violations, ...report.incomplete].filter((item) => item.impact === "critical" || item.impact === "serious")).toEqual([]);
  });
});
