import { describe, expect, it } from "vitest";
import { attentionGroups, planStatus } from "./tablePlanModel";
import type { TableRecord } from "./models";

const table = (overrides: Partial<TableRecord>): TableRecord => ({
  tableId: "t1", tableNumber: "M-1", zoneId: null, capacity: 4, active: true, status: "Available", currentOrderId: null,
  currentBillId: null, rowVersion: 1, allowedCommands: [], statusChangedAt: "2026-11-12T10:00:00Z", activeReservationId: null,
  reservationRowVersion: null, ...overrides,
});

describe("planStatus", () => {
  it.each([
    [{}, "free", "○", "Müsait"],
    [{ status: "Occupied" }, "busy", "●", "Dolu"],
    [{ status: "Occupied", currentBillId: "b" }, "pay", "₺", "Ödeme bekliyor"],
    [{ status: "Reserved" }, "rez", "◔", "Rezerve"],
    [{ status: "Cleaning" }, "clean", "✦", "Toplanıyor"],
    [{ status: "OutOfService" }, "off", "⊘", "Servis dışı"],
    [{ active: false }, "off", "⊘", "Servis dışı"],
  ] as const)("maps %j to %s", (overrides, key, icon, label) => {
    expect(planStatus(table(overrides as Partial<TableRecord>))).toEqual({ key, icon, label });
  });

  it("asks for the bill only for a table the till's list names, and a bill outranks it", () => {
    const awaiting = new Set(["t1"]);
    expect(planStatus(table({ status: "Cleaning" }), awaiting).key).toBe("ask");
    expect(planStatus(table({ status: "Cleaning" }), new Set(["other"])).key).toBe("clean");
    expect(planStatus(table({ status: "Cleaning", currentBillId: "b" }), awaiting).key).toBe("pay");
  });
});

describe("attentionGroups", () => {
  it("groups asking and paying tables and omits empty groups", () => {
    const tables = [table({ tableId: "a", tableNumber: "A", status: "Cleaning" }), table({ tableId: "b", tableNumber: "B", currentBillId: "x" }), table({ tableId: "c" })];
    expect(attentionGroups(tables, new Set(["a"])).map((group) => [group.key, group.tables.map((t) => t.tableNumber)])).toEqual([["ask", ["A"]], ["pay", ["B"]]]);
    expect(attentionGroups([tables[2]])).toEqual([]);
  });
});
