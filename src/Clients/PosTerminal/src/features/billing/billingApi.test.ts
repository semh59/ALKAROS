// @vitest-environment jsdom

import { describe, expect, it, vi } from "vitest";
import { createBillingSplitClient } from "./billingApi";
import type { BillSplitDesign } from "./models";

const design: BillSplitDesign = {
  billId: "bill/1", billNumber: "H-1042", billStatus: "Open", currencyCode: "TRY",
  payableAmount: 275, taxTotal: 25, billRowVersion: 7, mode: "None", executionState: "DesignOnly",
  allowedCommands: ["SaveEqual", "SaveItems", "SaveAmounts", "Clear"], items: [],
  allocations: [{ allocationId: "allocation-1", mode: "ByAmount", ownerKind: "Person", ownerId: "person-1", legacyOwnerReference: null, billItemId: null, quantity: null, amount: 275, taxAmount: 25, rowVersion: 3 }],
};

function response(body: unknown, status = 200): Response {
  return { ok: status >= 200 && status < 300, status, json: vi.fn().mockResolvedValue(body) } as unknown as Response;
}

describe("billing split client", () => {
  it("uses encoded terminal and bill routes with all concurrency versions", async () => {
    const fetcher = vi.fn().mockResolvedValue(response(design));
    const client = createBillingSplitClient("terminal/1", "bill/1", fetcher);
    await client.save({ mode: "ByAmount", targets: [{ owner: { kind: "Person", ownerId: "person-1" }, amount: 275 }] }, design);
    await client.clear(design);

    expect(fetcher).toHaveBeenNthCalledWith(1, "/api/v1/terminals/terminal%2F1/billing/bills/bill%2F1/split-design/amounts", expect.objectContaining({ method: "PUT", body: JSON.stringify({ expectedBillRowVersion: 7, expectedAllocations: [{ allocationId: "allocation-1", rowVersion: 3 }], targets: [{ owner: { kind: "Person", ownerId: "person-1" }, amount: 275 }] }) }));
    expect(fetcher).toHaveBeenNthCalledWith(2, "/api/v1/terminals/terminal%2F1/billing/bills/bill%2F1/split-design/clear", expect.objectContaining({ method: "POST" }));
  });

  it("preserves the server error envelope", async () => {
    const fetcher = vi.fn().mockResolvedValue(response({ error: { code: "CONCURRENT_MODIFICATION", message: "stale" } }, 409));
    await expect(createBillingSplitClient("terminal", "bill", fetcher).get()).rejects.toMatchObject({ status: 409, code: "CONCURRENT_MODIFICATION" });
  });
});
