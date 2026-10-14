// @vitest-environment jsdom

import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RefundPanel, type RefundPanelProps } from "./RefundPanel";
import {
  RefundApiError, type BillReceiptItem, type BillTenderSummary, type BillingRefundClient, type RefundAllowance, type RefundRequestRow,
} from "./refundApi";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const paid: BillTenderSummary = {
  allocatedTotal: 100, remainingAmount: 0,
  allocations: [{ allocationId: "a1", paymentId: "p1", amount: 100, paymentStatus: "Approved" }],
};
const unpaid: BillTenderSummary = { allocatedTotal: 0, remainingAmount: 100, allocations: [] };
const lineItem = (id: string, name: string, quantity: number, refundedQuantity = 0): BillReceiptItem => ({
  billItemId: id, name, quantity, unitPrice: 50, amount: 50 * quantity, refundedQuantity,
});
const shift = (allowance: number, used: number): RefundAllowance => ({
  hasOpenSession: true, allowance, used, remaining: allowance - used,
});
const request = (status: string, extra: Partial<RefundRequestRow> = {}): RefundRequestRow => ({
  requestId: "r1", billId: "b", paymentId: "p1", amount: 100, method: "Cash", reason: "Yanlış tutar", status, ...extra,
});

describe("refund panel", () => {
  let root: Root | null = null;
  const makeClient = (overrides: Partial<BillingRefundClient> = {}): BillingRefundClient => ({
    getTenders: vi.fn().mockResolvedValue(paid),
    listRequests: vi.fn().mockResolvedValue([]),
    cancelBill: vi.fn().mockResolvedValue(undefined),
    requestRefund: vi.fn().mockResolvedValue(request("Requested")),
    approve: vi.fn().mockResolvedValue(request("Completed")),
    reject: vi.fn().mockResolvedValue(request("Rejected")),
    complete: vi.fn().mockResolvedValue(request("Completed")),
    getAllowance: vi.fn().mockResolvedValue({ hasOpenSession: true, allowance: 0, used: 0, remaining: 0 }),
    getItems: vi.fn().mockResolvedValue([]),
    setAllowance: vi.fn().mockResolvedValue(shift(150, 0)),
    ...overrides,
  });
  async function render(props: Partial<RefundPanelProps> & { client: BillingRefundClient }) {
    document.documentElement.lang = "tr";
    document.title = "ALKAROS iade";
    document.body.innerHTML = '<main id="root"></main>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(
      <RefundPanel canCancel canRequestRefund canApproveRefund {...props} />,
    ));
  }
  async function click(label: string) {
    const button = [...document.querySelectorAll("button")].find((b) => b.textContent === label)!;
    await act(async () => button.click());
  }
  async function pick(label: string, option: string) {
    const select = [...document.querySelectorAll("label")].find((l) => l.textContent?.startsWith(label))!.querySelector("select")!;
    await act(async () => {
      select.value = option;
      select.dispatchEvent(new Event("change", { bubbles: true }));
    });
  }
  async function type(label: string, value: string) {
    const input = [...document.querySelectorAll("label")].find((l) => l.textContent?.startsWith(label))!.querySelector("input")!;
    await act(async () => {
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(input, value);
      input.dispatchEvent(new Event("input", { bubbles: true }));
    });
  }
  afterEach(async () => { if (root) await act(async () => root!.unmount()); root = null; vi.restoreAllMocks(); });

  it("sends a refund request with the typed reason for an approved payment", async () => {
    const client = makeClient();
    await render({ client });

    const ask = () => [...document.querySelectorAll("button")].find((b) => b.textContent === "İade iste")!;
    expect(ask().disabled).toBe(true);
    await pick("İade gerekçesi", "Yanlış sipariş");
    await click("İade iste");

    expect(client.requestRefund).toHaveBeenCalledWith("p1", "Yanlış sipariş");
    expect(document.body.textContent).toContain("İade isteği gönderildi");
    expect(client.getTenders).toHaveBeenCalledTimes(2);
  });

  it("offers no refund line for a payment that has an open request or is already refunded", async () => {
    const client = makeClient({
      getTenders: vi.fn().mockResolvedValue({
        ...paid,
        allocations: [...paid.allocations, { allocationId: "a2", paymentId: "p2", amount: 20, paymentStatus: "Refunded" }],
      }),
      listRequests: vi.fn().mockResolvedValue([request("Requested")]),
    });
    await render({ client });

    expect(document.body.textContent).not.toContain("İade gerekçesi");
    expect(document.body.textContent).toContain("Onay bekliyor");
  });

  it("offers no refund line for a payment that already waits for a manual refund, nor without the request permission", async () => {
    const waiting = makeClient({ listRequests: vi.fn().mockResolvedValue([request("AwaitingManualRefund", { method: "Manual" })]) });
    await render({ client: waiting });
    expect(document.body.textContent).not.toContain("İade gerekçesi");
    expect(document.body.textContent).not.toContain("Onayla");

    await act(async () => root!.unmount());
    await render({ client: makeClient(), canRequestRefund: false });
    expect(document.body.textContent).not.toContain("İade gerekçesi");
  });

  it("cancels a bill that has no money on it and then shows the cancelled message", async () => {
    const client = makeClient({ getTenders: vi.fn().mockResolvedValue(unpaid) });
    await render({ client });

    await pick("İptal gerekçesi", "Müşteri vazgeçti");
    await click("Hesabı iptal et");
    expect(client.cancelBill).not.toHaveBeenCalled();
    await click("Evet, hesabı iptal et");

    expect(client.cancelBill).toHaveBeenCalledWith("Müşteri vazgeçti");
    expect(document.body.textContent).toContain("Hesap iptal edildi.");
  });

  it("offers no cancel once the bill has an allocation, or without the cancel permission", async () => {
    await render({ client: makeClient() });
    expect(document.body.textContent).not.toContain("Hesabı iptal et");

    await act(async () => root!.unmount());
    await render({ client: makeClient({ getTenders: vi.fn().mockResolvedValue(unpaid) }), canCancel: false });
    expect(document.body.textContent).not.toContain("Hesabı iptal et");
  });

  it("lets an approver approve or reject, and shows the cancelled message when approval closed the bill", async () => {
    const approve = vi.fn().mockResolvedValue(request("Completed", { billCancelled: true }));
    const client = makeClient({ listRequests: vi.fn().mockResolvedValue([request("Requested")]), approve });
    await render({ client });

    expect(document.body.textContent).not.toContain("İadeyi tamamla");
    await pick("Ret gerekçesi", "Mutfak hatası");
    await click("Reddet");
    expect(client.reject).toHaveBeenCalledWith("r1", "Mutfak hatası");

    await click("Onayla");
    expect(approve).not.toHaveBeenCalled();
    await click("Evet, iadeyi onayla");
    expect(approve).toHaveBeenCalledWith("r1");
    expect(document.body.textContent).toContain("Hesap iptal edildi.");
  });

  it("hides approve and reject without the approve permission", async () => {
    const client = makeClient({ listRequests: vi.fn().mockResolvedValue([request("Requested")]) });
    await render({ client, canApproveRefund: false });

    expect(document.body.textContent).toContain("Onay bekliyor");
    expect(document.body.textContent).not.toContain("Onayla");
    expect(document.body.textContent).not.toContain("Reddet");
  });

  it("completes a manual refund with the slip number", async () => {
    const client = makeClient({
      listRequests: vi.fn().mockResolvedValue([request("AwaitingManualRefund", { method: "Manual" })]),
    });
    await render({ client });
    expect(document.body.textContent).toContain("Elle iade bekliyor");
    expect(document.body.textContent).toContain("Kart / elle iade");

    const complete = () => [...document.querySelectorAll("button")].find((b) => b.textContent === "İadeyi tamamla")!;
    expect(complete().disabled).toBe(true);
    await type("İade fiş numarası", "POS-4471");
    await click("İadeyi tamamla");

    expect(client.complete).toHaveBeenCalledWith("r1", "POS-4471");
  });

  it("shows a fixed Turkish sentence for a refusal, never the raw status or server text", async () => {
    const client = makeClient({
      requestRefund: vi.fn().mockRejectedValue(new RefundApiError(409, "REFUND_ALREADY_REQUESTED", "raw 409 text")),
    });
    await render({ client });

    await pick("İade gerekçesi", "İkram");
    await click("İade iste");

    expect(document.body.textContent).toContain("Güncel veriyi alıp yeniden deneyin");
    expect(document.body.textContent).not.toMatch(/409|raw/);
  });

  it("shows a retryable error when the bill data cannot be read", async () => {
    const getTenders = vi.fn().mockRejectedValueOnce(new RefundApiError(0, "NETWORK_UNAVAILABLE", "")).mockResolvedValue(paid);
    await render({ client: makeClient({ getTenders }) });
    expect(document.body.textContent).toContain("İptal ve iade bilgileri okunamadı.");

    await click("Yenile");

    expect(getTenders).toHaveBeenCalledTimes(2);
    expect(document.body.textContent).toContain("İade iste");
  });

  it("keeps cancel, refund and reject buttons off until their reason is typed, and lets the user back out of a cancel", async () => {
    const client = makeClient({
      getTenders: vi.fn().mockResolvedValue({ ...unpaid, allocations: [] }),
      listRequests: vi.fn().mockResolvedValue([request("Requested", { paymentId: "p9" })]),
    });
    await render({ client });
    const button = (label: string) => [...document.querySelectorAll("button")].find((b) => b.textContent === label)!;
    expect(button("Hesabı iptal et").disabled).toBe(true);
    expect(button("Reddet").disabled).toBe(true);

    await pick("İptal gerekçesi", "Müşteri vazgeçti");
    expect(button("Hesabı iptal et").disabled).toBe(false);
    await click("Hesabı iptal et");
    await click("Vazgeç");

    expect(client.cancelBill).not.toHaveBeenCalled();
    expect(document.body.textContent).not.toContain("Emin misiniz");
  });

  it("offers no refund line while the request list is unknown, but a 403 on the list is not a failure", async () => {
    const failing = makeClient({ listRequests: vi.fn().mockRejectedValue(new RefundApiError(500, "REQUEST_FAILED", "")) });
    await render({ client: failing });
    expect(document.body.textContent).toContain("İade istekleri okunamadı");
    expect(document.body.textContent).not.toContain("İade gerekçesi");

    await act(async () => root!.unmount());
    const forbidden = makeClient({ listRequests: vi.fn().mockRejectedValue(new RefundApiError(403, "FORBIDDEN", "")) });
    await render({ client: forbidden });
    expect(document.body.textContent).not.toContain("İade istekleri okunamadı");
    expect(document.body.textContent).toContain("İade gerekçesi");
  });

  it("offers no cancel while an unsettled payment exists", async () => {
    const client = makeClient({ getTenders: vi.fn().mockResolvedValue({ ...unpaid, unsettledPayment: { paymentId: "p1" } }) });
    await render({ client });

    expect(document.body.textContent).not.toContain("Hesabı iptal et");
  });

  it("reloads after a conflict, and keeps the panel when a reload fails after an action", async () => {
    const getTenders = vi.fn().mockResolvedValue(paid);
    const requestRefund = vi.fn().mockRejectedValue(new RefundApiError(409, "REFUND_STATE", ""));
    await render({ client: makeClient({ getTenders, requestRefund }) });
    await pick("İade gerekçesi", "İkram");
    await click("İade iste");
    expect(getTenders).toHaveBeenCalledTimes(2);

    await act(async () => root!.unmount());
    const flaky = vi.fn().mockResolvedValueOnce(paid).mockRejectedValue(new RefundApiError(0, "NETWORK_UNAVAILABLE", ""));
    await render({ client: makeClient({ getTenders: flaky }) });
    await pick("İade gerekçesi", "İkram");
    await click("İade iste");

    expect(document.body.textContent).toContain("İade gerekçesi");
    expect(document.body.textContent).toContain("Sunucuya ulaşılamadı");
  });

  it("opens a free text field for Diğer and keeps the button off until it is filled", async () => {
    const client = makeClient({ getTenders: vi.fn().mockResolvedValue(unpaid) });
    await render({ client });
    const cancel = () => [...document.querySelectorAll("button")].find((b) => b.textContent === "Hesabı iptal et")!;
    expect(document.body.textContent).not.toContain("Gerekçeyi yazın");

    await pick("İptal gerekçesi", "Diğer");
    expect(document.body.textContent).toContain("Gerekçeyi yazın");
    expect(cancel().disabled).toBe(true);
    await type("Gerekçeyi yazın", "Masa değişti");
    expect(cancel().disabled).toBe(false);
    await click("Hesabı iptal et");
    await click("Evet, hesabı iptal et");

    expect(client.cancelBill).toHaveBeenCalledWith("Masa değişti");
  });

  it("offers the same preset list everywhere", async () => {
    const client = makeClient({ getTenders: vi.fn().mockResolvedValue(unpaid) });
    await render({ client });

    const options = [...document.querySelectorAll("select option")].map((option) => option.textContent);

    expect(options).toEqual(["Gerekçe seçin", "Müşteri vazgeçti", "Yanlış sipariş", "Mutfak hatası", "Ürün sorunlu", "İkram", "Yanlış tutar girildi", "Diğer"]);
  });

  it("says a refund inside the shift allowance was completed from it, and that a card one waits for the slip", async () => {
    const done = makeClient({
      requestRefund: vi.fn().mockResolvedValue(request("Completed", { approvedFromAllowance: true })),
    });
    await render({ client: done });
    await pick("İade gerekçesi", "İkram");
    await click("İade iste");
    expect(document.body.textContent).toContain("İade vardiya bütçenizden tamamlandı.");
    expect(document.body.textContent).not.toContain("onay bekliyor");

    await act(async () => root!.unmount());
    const card = makeClient({
      requestRefund: vi.fn().mockResolvedValue(request("AwaitingManualRefund", { method: "Manual", approvedFromAllowance: true })),
    });
    await render({ client: card });
    await pick("İade gerekçesi", "İkram");
    await click("İade iste");
    expect(document.body.textContent).toContain("fiş numarasını girerek tamamlayın");
  });

  it("shows the remaining shift allowance only when there is one", async () => {
    await render({ client: makeClient({ getAllowance: vi.fn().mockResolvedValue(shift(150, 100)) }) });
    expect(document.body.textContent).toContain("Kalan vardiya iade bütçeniz");
    expect(document.body.textContent).toContain("50,00");

    await act(async () => root!.unmount());
    await render({ client: makeClient() });
    expect(document.body.textContent).not.toContain("Kalan vardiya iade bütçeniz");

    await act(async () => root!.unmount());
    await render({ client: makeClient({ getAllowance: vi.fn().mockResolvedValue({ ...shift(150, 0), hasOpenSession: false }) }) });
    expect(document.body.textContent).not.toContain("Kalan vardiya iade bütçeniz");

    await act(async () => root!.unmount());
    await render({ client: makeClient({ getAllowance: vi.fn().mockResolvedValue(shift(150, 0)) }), canRequestRefund: false });
    expect(document.body.textContent).not.toContain("Kalan vardiya iade bütçeniz");

    await act(async () => root!.unmount());
    await render({ client: makeClient({ getAllowance: vi.fn().mockRejectedValue(new RefundApiError(500, "X", "x")) }) });
    expect(document.body.textContent).not.toContain("Kalan vardiya iade bütçeniz");
    expect(document.body.textContent).toContain("İade iste");
  });

  it("lets an approver give the requester a shift allowance from a waiting request", async () => {
    const client = makeClient({ listRequests: vi.fn().mockResolvedValue([request("Requested", { requestedBy: "u-cashier" })]) });
    await render({ client });
    const give = () => [...document.querySelectorAll("button")].find((b) => b.textContent === "Bütçe ver")!;
    expect(give().disabled).toBe(true);

    await type("Bu kasiyerin vardiya iade bütçesi", "abc");
    expect(give().disabled).toBe(true);
    await type("Bu kasiyerin vardiya iade bütçesi", "-5");
    expect(give().disabled).toBe(true);
    await type("Bu kasiyerin vardiya iade bütçesi", "150,5");
    await click("Bütçe ver");

    expect(client.setAllowance).toHaveBeenCalledWith("u-cashier", 150.5);
    expect(document.body.textContent).toContain("Kasiyerin vardiya iade bütçesi güncellendi.");
  });

  it("offers no allowance field without the approve permission or without a known requester", async () => {
    const rows = vi.fn().mockResolvedValue([request("Requested", { requestedBy: "u-cashier" })]);
    await render({ client: makeClient({ listRequests: rows }), canApproveRefund: false });
    expect(document.body.textContent).not.toContain("Bütçe ver");

    await act(async () => root!.unmount());
    await render({ client: makeClient({ listRequests: vi.fn().mockResolvedValue([request("Requested")]) }) });
    expect(document.body.textContent).not.toContain("Bütçe ver");
  });

  it("explains an allowance refusal with a fixed sentence", async () => {
    const client = makeClient({
      listRequests: vi.fn().mockResolvedValue([request("Requested", { requestedBy: "u-cashier" })]),
      setAllowance: vi.fn().mockRejectedValue(new RefundApiError(403, "SELF_DECISION", "x")),
    });
    await render({ client });
    await type("Bu kasiyerin vardiya iade bütçesi", "100");
    await click("Bütçe ver");

    expect(document.body.textContent).toContain("Kendinize iade bütçesi veremezsiniz.");
    expect(document.body.textContent).not.toContain("SELF_DECISION");
  });

  it("refunds chosen units of items from a chosen payment with the stock choice and shows the computed amount", async () => {
    const client = makeClient({
      getItems: vi.fn().mockResolvedValue([lineItem("i1", "Çay", 3, 1), lineItem("i2", "Tost", 1)]),
      requestRefund: vi.fn().mockResolvedValue(request("Requested", { amount: 90, items: [{ billItemId: "i1", quantity: 2, restoreStock: true }] })),
    });
    await render({ client });
    const action = () => [...document.querySelectorAll("button")].find((b) => b.textContent === "Seçilen ürünleri iade et")!;
    expect(document.body.textContent).toContain("Çay · kalan 2");
    expect(action().disabled).toBe(true);

    await type("Çay · kalan 2", "2");
    await pick("İadenin düşeceği ödeme", "p1");
    await pick("Stoğa geri dönüş", "yes");
    expect(action().disabled).toBe(true);
    const reason = [...document.querySelectorAll("select")].filter((s) => s.closest("label")?.textContent?.startsWith("İade gerekçesi"))[1]!;
    await act(async () => {
      reason.value = "İkram";
      reason.dispatchEvent(new Event("change", { bubbles: true }));
    });
    expect(action().disabled).toBe(false);
    await act(async () => action().click());

    expect(client.requestRefund).toHaveBeenCalledWith("p1", "İkram", [{ billItemId: "i1", quantity: 2, restoreStock: true }]);
    expect(document.body.textContent).toContain("İade tutarı");
  });

  it("needs every piece before the item refund can be sent and sends an explicit no for stock", async () => {
    const requestRefund = vi.fn().mockResolvedValue(request("Requested", { amount: 50 }));
    const client = makeClient({ getItems: vi.fn().mockResolvedValue([lineItem("i1", "Çay", 3)]), requestRefund });
    await render({ client });
    const action = () => [...document.querySelectorAll("button")].find((b) => b.textContent === "Seçilen ürünleri iade et")!;
    const reasonSelect = () => [...document.querySelectorAll("select")]
      .filter((s) => s.closest("label")?.textContent?.startsWith("İade gerekçesi"))[1]!;
    const setSelect = async (select: HTMLSelectElement, value: string) => act(async () => {
      select.value = value;
      select.dispatchEvent(new Event("change", { bubbles: true }));
    });

    await type("Çay · kalan 3", "1");
    await pick("Stoğa geri dönüş", "no");
    await setSelect(reasonSelect(), "İkram");
    expect(action().disabled).toBe(true);
    await pick("İadenin düşeceği ödeme", "p1");
    expect(action().disabled).toBe(false);
    await pick("Stoğa geri dönüş", "");
    expect(action().disabled).toBe(true);
    await pick("Stoğa geri dönüş", "no");
    await click("Seçilen ürünleri iade et");

    expect(requestRefund).toHaveBeenCalledWith("p1", "İkram", [{ billItemId: "i1", quantity: 1, restoreStock: false }]);
  });

  it("blocks a quantity above what is left and keeps the stock choice explicit", async () => {
    const client = makeClient({ getItems: vi.fn().mockResolvedValue([lineItem("i1", "Çay", 3, 1)]) });
    await render({ client });
    const action = () => [...document.querySelectorAll("button")].find((b) => b.textContent === "Seçilen ürünleri iade et")!;

    await type("Çay · kalan 2", "3");
    await pick("İadenin düşeceği ödeme", "p1");
    await pick("Stoğa geri dönüş", "no");

    expect(action().disabled).toBe(true);
  });

  it("offers a partly refunded payment and hides the block when nothing is left or no payment can take it", async () => {
    const partly: BillTenderSummary = {
      ...paid, allocations: [{ allocationId: "a1", paymentId: "p1", amount: 100, paymentStatus: "PartiallyRefunded" }],
    };
    await render({ client: makeClient({ getTenders: vi.fn().mockResolvedValue(partly), getItems: vi.fn().mockResolvedValue([lineItem("i1", "Çay", 3)]) }) });
    expect(document.body.textContent).toContain("Ürün bazlı iade");

    await act(async () => root!.unmount());
    await render({ client: makeClient({ getItems: vi.fn().mockResolvedValue([lineItem("i1", "Çay", 3, 3)]) }) });
    expect(document.body.textContent).not.toContain("Ürün bazlı iade");

    await act(async () => root!.unmount());
    await render({ client: makeClient({ getTenders: vi.fn().mockResolvedValue(unpaid), getItems: vi.fn().mockResolvedValue([lineItem("i1", "Çay", 3)]) }) });
    expect(document.body.textContent).not.toContain("Ürün bazlı iade");

    await act(async () => root!.unmount());
    await render({ client: makeClient({ getItems: vi.fn().mockResolvedValue([lineItem("i1", "Çay", 3)]) }), canRequestRefund: false });
    expect(document.body.textContent).not.toContain("Ürün bazlı iade");
  });

  it("keeps the whole-payment refund working when the item list cannot be read", async () => {
    await render({ client: makeClient({ getItems: vi.fn().mockRejectedValue(new RefundApiError(500, "X", "x")) }) });

    expect(document.body.textContent).toContain("İade iste");
    expect(document.body.textContent).not.toContain("Ürün bazlı iade");
  });

  it("has no accessibility violations", async () => {
    await render({ client: makeClient({ listRequests: vi.fn().mockResolvedValue([request("Requested")]) }) });

    const result = await axe.run(document.body);

    expect(result.violations).toEqual([]);
  });
});
