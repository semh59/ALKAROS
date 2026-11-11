// @vitest-environment jsdom

import { describe, expect, it, vi } from "vitest";
import { createBillingSplitClient, describeSplitFailure } from "./billingApi";
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

  it("keeps the status and code of the server error but replaces its text with a fixed Turkish sentence", async () => {
    const fetcher = vi.fn().mockResolvedValue(response({ error: { code: "CONCURRENT_MODIFICATION", message: "stale" } }, 409));
    const failure = createBillingSplitClient("terminal", "bill", fetcher).get();
    await expect(failure).rejects.toMatchObject({ status: 409, code: "CONCURRENT_MODIFICATION" });
    await expect(failure).rejects.toThrow("başka bir işlem tarafından değiştirildi");
    await expect(failure).rejects.not.toThrow("stale");
  });

  it("gives every known split error code and every status a Turkish sentence of its own", () => {
    const codes = [
      "UNAUTHORIZED", "FORBIDDEN", "NOT_FOUND", "GRANT_DENIED", "UNSUPPORTED_ORDER_STATE", "CONCURRENT_MODIFICATION",
      "UNSUPPORTED_BILL_STATE", "DISCOUNT_BELOW_COLLECTED", "ITEM_SPLIT_ON_ADJUSTED_BILL", "BILL_HAS_PAYMENT",
      "DISCOUNT_REASON_INVALID", "DISCOUNT_TYPE_INVALID", "DISCOUNT_VALUE_INVALID", "IDEMPOTENCY_KEY_INVALID",
      "NOTES_TOO_LONG", "TIP_AMOUNT_INVALID", "MERGE_NEEDS_TWO_ORDERS", "MERGE_TOO_MANY_ORDERS", "MERGE_DIFFERENT_TABLES",
      "MERGE_ORDER_SOURCE", "MERGE_ORDER_ALREADY_BILLED", "MERGE_MIXED_SENT", "MERGE_ORDER_EMPTY", "IDEMPOTENCY_KEY_REQUIRED",
      "IDEMPOTENCY_KEY_REUSED",
    ];
    const sentences = codes.map((code) => describeSplitFailure(500, code));
    expect(new Set(sentences).size).toBe(codes.length);
    for (const sentence of sentences) expect(sentence).toMatch(/[ıİşŞğĞüÜöÖçÇ]/);
    expect(describeSplitFailure(409, "UNSUPPORTED_BILL_STATE")).toBe("Bu hesap durumunda bölme tasarımı değiştirilemez.");
    expect(describeSplitFailure(0, "X")).toContain("Sunucuya ulaşılamadı");
    expect(describeSplitFailure(401, "X")).toContain("Oturum");
    expect(describeSplitFailure(403, "X")).toContain("yetkiniz yok");
    expect(describeSplitFailure(404, "X")).toContain("bulunamadı");
    expect(describeSplitFailure(409, "X")).toContain("yenileyip");
    expect(describeSplitFailure(400, "X")).toContain("geçersiz");
    expect(describeSplitFailure(500, "X")).toBe("Hesap dağıtımı kaydedilemedi.");
  });
});
