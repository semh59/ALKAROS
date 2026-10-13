// @vitest-environment jsdom

import { describe, expect, it, vi } from "vitest";
import { RefundApiError, createBillingRefundClient, describeRefundFailure } from "./refundApi";

function response(body: unknown, status = 200): Response {
  return { ok: status >= 200 && status < 300, status, json: vi.fn().mockResolvedValue(body) } as unknown as Response;
}

const row = (billId: string) => ({ requestId: `r-${billId}`, billId, paymentId: "p1", amount: 10, method: "Cash", reason: "x", status: "Requested" });

describe("billing refund client", () => {
  it("posts the cancel reason and the refund request to encoded bill routes", async () => {
    const fetcher = vi.fn().mockResolvedValue(response({}));
    const client = createBillingRefundClient("term/1", "bill/1", fetcher);
    await client.cancelBill("Müşteri vazgeçti");
    await client.requestRefund("pay-1", "Fazla ödeme");

    expect(fetcher).toHaveBeenNthCalledWith(1, "/api/v1/terminals/term%2F1/billing/bills/bill%2F1/cancel",
      expect.objectContaining({ method: "POST", body: JSON.stringify({ reason: "Müşteri vazgeçti" }) }));
    expect(fetcher).toHaveBeenNthCalledWith(2, "/api/v1/terminals/term%2F1/billing/bills/bill%2F1/refund-requests",
      expect.objectContaining({ method: "POST", body: JSON.stringify({ paymentId: "pay-1", reason: "Fazla ödeme" }) }));
  });

  it("decides and completes a request on the request route", async () => {
    const fetcher = vi.fn().mockResolvedValue(response({}));
    const client = createBillingRefundClient("t", "b", fetcher);
    await client.approve("r1");
    await client.reject("r1", "Yetersiz");
    await client.complete("r1", "POS-1");

    expect(fetcher).toHaveBeenNthCalledWith(1, "/api/v1/terminals/t/billing/refund-requests/r1/approve", expect.objectContaining({ method: "POST" }));
    expect(fetcher).toHaveBeenNthCalledWith(2, "/api/v1/terminals/t/billing/refund-requests/r1/reject",
      expect.objectContaining({ body: JSON.stringify({ note: "Yetersiz" }) }));
    expect(fetcher).toHaveBeenNthCalledWith(3, "/api/v1/terminals/t/billing/refund-requests/r1/complete",
      expect.objectContaining({ body: JSON.stringify({ slipNumber: "POS-1", note: null }) }));
  });

  it("keeps only the refund requests of its own bill", async () => {
    const fetcher = vi.fn().mockResolvedValue(response([row("b"), row("other")]));

    const rows = await createBillingRefundClient("t", "b", fetcher).listRequests();

    expect(rows.map((r) => r.billId)).toEqual(["b"]);
  });

  it("carries the status and code of a refusal and reports an unreachable server as status 0", async () => {
    const refused = vi.fn().mockResolvedValue(response({ error: { code: "SELF_DECISION", message: "x" } }, 403));
    await expect(createBillingRefundClient("t", "b", refused).approve("r")).rejects.toMatchObject({ status: 403, code: "SELF_DECISION" });

    const offline = vi.fn().mockRejectedValue(new TypeError("fetch failed"));
    await expect(createBillingRefundClient("t", "b", offline).approve("r")).rejects.toMatchObject({ status: 0 });
  });

  it("describes failures with fixed Turkish sentences only", () => {
    const text = (status: number, code = "") => describeRefundFailure(new RefundApiError(status, code, "raw server text"));

    expect(text(0)).toContain("Sunucuya ulaşılamadı");
    expect(text(401)).toContain("Yeniden giriş");
    expect(text(403, "SELF_DECISION")).toContain("Kendi açtığınız");
    expect(text(403)).toContain("yetkiniz yok");
    expect(text(400)).toContain("eksik");
    expect(text(409, "NO_OPEN_CASH_SESSION")).toContain("kasa oturumu");
    expect(text(409, "ACCOUNT_REFUND_UNSUPPORTED")).toContain("Cari hesaba");
    expect(text(409, "REFUND_STATE")).toContain("Güncel veriyi alıp");
    expect(text(500)).toContain("tamamlanamadı");
    for (const status of [0, 401, 403, 400, 409, 500]) expect(text(status)).not.toMatch(/raw server text|\d{3}/);
  });
});
