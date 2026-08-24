import { describe, expect, it } from "vitest";
import type { DisplaySnapshot } from "./contracts";
import { afterFailure, afterSnapshot } from "./stale";

const snapshot: DisplaySnapshot = {
  displayId: "display",
  terminalId: "terminal",
  orderId: "order",
  revision: 3,
  state: "Active",
  editable: true,
  orderNumber: "POS-1",
  lines: [],
  subtotal: 10,
  discountTotal: 0,
  taxTotal: 2,
  total: 12,
  currency: "TRY",
  serverTimestamp: "2026-08-24T00:00:00Z",
  message: "Aktif",
};

describe("customer display freshness", () => {
  it("keeps the last snapshot under ten seconds with a connection warning", () => {
    const fresh = afterSnapshot(snapshot, 1_000);
    const failed = afterFailure(fresh, 10_999);
    expect(failed.snapshot).toBe(snapshot);
    expect(failed.connectionLost).toBe(true);
  });

  it("clears stale monetary data at ten seconds", () => {
    const fresh = afterSnapshot(snapshot, 1_000);
    const failed = afterFailure(fresh, 11_000);
    expect(failed.snapshot).toBeNull();
    expect(failed.connectionLost).toBe(true);
  });
});
