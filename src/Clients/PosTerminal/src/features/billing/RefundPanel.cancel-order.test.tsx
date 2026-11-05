// @vitest-environment jsdom

import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RefundPanel } from "./RefundPanel";
import { RefundApiError, type BillingRefundClient } from "./refundApi";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

describe("cancel the order together with the bill", () => {
  let root: Root | null = null;
  const makeClient = (cancelBill: BillingRefundClient["cancelBill"]): BillingRefundClient => ({
    getTenders: vi.fn().mockResolvedValue({ allocatedTotal: 0, remainingAmount: 100, allocations: [] }),
    listRequests: vi.fn().mockResolvedValue([]),
    cancelBill,
    requestRefund: vi.fn(),
    approve: vi.fn(),
    reject: vi.fn(),
    complete: vi.fn(),
    getAllowance: vi.fn().mockResolvedValue({ hasOpenSession: false, allowance: 0, used: 0, remaining: 0 }),
    getItems: vi.fn().mockResolvedValue([]),
    setAllowance: vi.fn(),
  });
  async function render(client: BillingRefundClient) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<main id="root"></main>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(<RefundPanel client={client} canCancel canRequestRefund canApproveRefund />));
  }
  const button = (label: string) => [...document.querySelectorAll("button")].find((b) => b.textContent === label)!;
  const checkbox = () => document.querySelector<HTMLInputElement>('input[type="checkbox"]')!;
  async function chooseReason() {
    const select = [...document.querySelectorAll("label")].find((l) => l.textContent?.startsWith("İptal gerekçesi"))!.querySelector("select")!;
    await act(async () => {
      select.value = "Müşteri vazgeçti";
      select.dispatchEvent(new Event("change", { bubbles: true }));
    });
  }
  async function confirmCancel() {
    await act(async () => button("Hesabı iptal et").click());
    await act(async () => [...document.querySelectorAll("button")].find((b) => b.textContent?.startsWith("Evet, hesabı"))!.click());
  }
  afterEach(async () => { if (root) await act(async () => root!.unmount()); root = null; vi.restoreAllMocks(); });

  it("offers the box unchecked, explains what it does and keeps the order when it stays unchecked", async () => {
    const cancelBill = vi.fn().mockResolvedValue({ orderCancelled: false });
    await render(makeClient(cancelBill));

    expect(checkbox().checked).toBe(false);
    expect(document.body.textContent).toContain("Siparişi de iptal et");
    expect(document.body.textContent).toContain("fire sayılır");
    await chooseReason();
    await confirmCancel();

    expect(cancelBill).toHaveBeenCalledWith("Müşteri vazgeçti", false);
    expect(document.body.textContent).toContain("Hesap iptal edildi.");
    expect(document.body.textContent).not.toContain("sipariş iptal edildi");
  });

  it("sends the flag when the box is checked and says that the order was cancelled too", async () => {
    const cancelBill = vi.fn().mockResolvedValue({ orderCancelled: true });
    await render(makeClient(cancelBill));

    await chooseReason();
    await act(async () => checkbox().click());
    await confirmCancel();

    expect(cancelBill).toHaveBeenCalledWith("Müşteri vazgeçti", true);
    expect(document.body.textContent).toContain("Hesap ve sipariş iptal edildi; masa boşaldı.");
  });

  it("asks again when the box changes after the first tap", async () => {
    const cancelBill = vi.fn().mockResolvedValue({ orderCancelled: true });
    await render(makeClient(cancelBill));

    await chooseReason();
    await act(async () => button("Hesabı iptal et").click());
    expect(button("Evet, hesabı iptal et")).toBeDefined();
    await act(async () => checkbox().click());

    expect(button("Evet, hesabı iptal et")).toBeUndefined();
    await act(async () => button("Hesabı iptal et").click());
    expect(button("Evet, hesabı ve siparişi iptal et")).toBeDefined();
    expect(cancelBill).not.toHaveBeenCalled();
  });

  it("does not cancel before the second confirmation tap", async () => {
    const cancelBill = vi.fn().mockResolvedValue({ orderCancelled: true });
    await render(makeClient(cancelBill));

    await chooseReason();
    await act(async () => checkbox().click());
    await act(async () => button("Hesabı iptal et").click());

    expect(cancelBill).not.toHaveBeenCalled();
  });

  it("answers a refused order cancellation with the fixed sentence and keeps the screen open", async () => {
    const cancelBill = vi.fn().mockRejectedValue(new RefundApiError(409, "ORDER_HAS_SERVED_ITEMS", "x"));
    await render(makeClient(cancelBill));

    await chooseReason();
    await act(async () => checkbox().click());
    await confirmCancel();

    expect(document.body.textContent).toContain("Servis edilmiş ürün var; sipariş iptal edilemez, hesabı kapatın.");
    expect(document.body.textContent).not.toContain("ORDER_HAS_SERVED_ITEMS");
    expect(button("Hesabı iptal et")).toBeDefined();
  });
});
