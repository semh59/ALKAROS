// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { registerSessionExpiredHandler } from "../api";
import { formatMoney } from "../format";
import { TableOrderBridge } from "./TableOrderBridge";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root | null = null;
const reply = (status: number, body: unknown) => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });
const tableOrder = {
  orderId: "o-1", tableId: "tb-4", tableNumber: "T-4", status: "Submitted", rowVersion: 3, totalAmount: 150, createdAt: "2026-11-12T10:00:00Z",
  items: [
    { itemId: "i1", productName: "Çay", quantity: 2, unitPrice: 25, totalPrice: 50, status: "Active", kitchenState: "Sent" },
    { itemId: "i2", productName: "Köfte", quantity: 1, unitPrice: 100, totalPrice: 100, status: "Active", kitchenState: "Sent" },
    { itemId: "i3", productName: "Yanlış ürün", quantity: 1, unitPrice: 9, totalPrice: 9, status: "Cancelled", kitchenState: "NotSent" },
  ],
};
const stubFetch = (other: () => Response = () => reply(500, {}), order: unknown = tableOrder) => {
  const mock = vi.fn(async (url: string, init?: RequestInit) => String(url).includes("/orders/table/") && init?.method !== "POST" ? reply(200, order) : other());
  vi.stubGlobal("fetch", mock);
  return mock;
};
const otherCalls = (mock: ReturnType<typeof stubFetch>) => mock.mock.calls.filter(([url]) => !String(url).includes("/orders/table/"));
beforeEach(() => { stubFetch(); });
afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
});

const render = (props: Partial<Parameters<typeof TableOrderBridge>[0]> = {}) => {
  const onStartOrder = vi.fn();
  const onAddWithoutKitchen = vi.fn();
  const onSent = vi.fn();
  const onTransfer = vi.fn();
  const navigateTo = vi.fn();
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  act(() => root!.render(createElement(TableOrderBridge, {
    terminalId: "t-1", table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: null, rowVersion: 1 },
    orderAllowed: true, canManage: true, canCollect: true, canReadLines: true, canAddWithoutKitchen: true, busy: false, onStartOrder, onAddWithoutKitchen, onSent, onTransfer, navigateTo, ...props,
  })));
  return { onStartOrder, onAddWithoutKitchen, onSent, onTransfer, navigateTo };
};
const button = (label: string) => [...document.querySelectorAll("button")].find((b) => b.textContent === label);

describe("TableOrderBridge", () => {
  const flush = () => act(async () => { await Promise.resolve(); });
  const click = (label: string) => act(async () => { button(label)!.dispatchEvent(new MouseEvent("click", { bubbles: true })); });

  it("Öde opens the order's bill and goes to the payment page", async () => {
    stubFetch(() => reply(200, { billId: "b-new" }));
    const { navigateTo } = render();

    await click("Öde");

    expect(navigateTo).toHaveBeenCalledWith("/cashier/payments/split-payment/index.html?billId=b-new");
  });

  describe("Hesap fişi", () => {
    const slip = { orderId: "o-1", printNumber: 3, printedAt: "2026-11-18T10:00:00Z", tableNumber: "T-4", currencyCode: "TRY", lines: [{ name: "Çay", quantity: 2, unitPrice: 25, amount: 50 }], adjustments: [], total: 50, paidAmount: 0 };
    const print = vi.fn();
    beforeEach(() => { print.mockReset(); Object.defineProperty(window, "print", { value: print, configurable: true, writable: true }); });
    afterEach(() => document.getElementById("pre-receipt-print")?.remove());

    it("prints the slip of the table's order with one tap and opens no bill", async () => {
      const fetchMock = stubFetch(() => reply(200, slip));
      const { navigateTo } = render();

      await click("Hesap fişi");

      expect(otherCalls(fetchMock).map(([url, init]) => `${init?.method} ${url}`)).toEqual(["POST /api/v1/terminals/t-1/orders/o-1/pre-receipt"]);
      expect(print).toHaveBeenCalledTimes(1);
      expect(document.getElementById("pre-receipt-print")?.textContent).toContain("MALİ DEĞERİ YOKTUR");
      expect(navigateTo).not.toHaveBeenCalled();
    });

    it("says in Turkish why a slip was refused and prints nothing", async () => {
      stubFetch(() => reply(409, { error: { code: "BILL_NOT_UNPAID", message: "English text" } }));
      render();

      await click("Hesap fişi");

      expect(document.querySelector(".table-order-bridge__error")?.textContent).toBe("Bu hesap ödenmiş; ödenmiş hesabın fişini Hesap bölme ekranından yazdırın.");
      expect(print).not.toHaveBeenCalled();
    });

    it("is not offered to a user who cannot take payments", () => {
      render({ canCollect: false });

      expect(button("Hesap fişi")).toBeUndefined();
    });
  });

  it("Öde goes straight to a bill the table already has", async () => {
    const fetchMock = stubFetch();
    const { navigateTo } = render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: null, currentBillId: "b-1", rowVersion: 1 } });

    await click("Öde");

    expect(navigateTo).toHaveBeenCalledWith("/cashier/payments/split-payment/index.html?billId=b-1");
    expect(otherCalls(fetchMock)).toHaveLength(0);
  });

  it("a refused bill opening shows a Turkish message and stays on the screen", async () => {
    stubFetch(() => reply(409, { error: { code: "ORDER_NOT_SUBMITTED" } }));
    const { navigateTo } = render();

    await click("Öde");

    expect(navigateTo).not.toHaveBeenCalled();
    expect(document.querySelector('[role="alert"]')?.textContent?.length).toBeGreaterThan(5);
  });

  it("a session that ended while opening the bill is reported to the session handler", async () => {
    stubFetch(() => reply(401, { error: { code: "UNAUTHORIZED" } }));
    const ended = vi.fn();
    registerSessionExpiredHandler(ended);
    render();

    await click("Öde");
    registerSessionExpiredHandler(null);

    expect(ended).toHaveBeenCalledTimes(1);
  });

  it("draws no order button for someone who may not open orders, and keeps Öde", () => {
    render({ canManage: false });

    expect(button("Ürün ekle")).toBeUndefined();
    expect(button("Sipariş al")).toBeUndefined();
    expect(button("Öde")).toBeDefined();
    expect(document.querySelector(".table-order-bridge")?.textContent).toContain("Bu masadaki hesabı ödeme ekranından alın.");
    expect(document.querySelector(".table-order-bridge")?.textContent).not.toContain("devam edin");
  });

  it("tells someone who may not open orders that an empty table has nothing for them, with no buttons and no table-card hint", () => {
    render({ canManage: false, orderAllowed: false, table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: null, currentBillId: null, rowVersion: 1 } });

    expect(document.querySelectorAll("button")).toHaveLength(0);
    expect(document.querySelector(".table-order-bridge")?.textContent).toContain("Bu masada sizin yapabileceğiniz bir işlem yok.");
    expect(document.querySelector(".table-order-bridge")?.textContent).not.toContain("uygun değil");
  });

  it("does not offer a bill nobody may open: a non-manager without the bill permission gets no button and no promise", () => {
    render({ canManage: false, canCollect: false, table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1 } });

    expect(document.querySelectorAll("button")).toHaveLength(0);
    expect(document.querySelector(".table-order-bridge")?.textContent).toContain("Bu masada sizin yapabileceğiniz bir işlem yok.");
  });

  it("closes the continue button on a table whose bill is open and says why and how to get out", () => {
    const { onStartOrder } = render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1 } });

    expect(button("Ürün ekle")?.disabled).toBe(true);
    expect(document.querySelector(".table-order-bridge")?.textContent).toContain("ürün eklenemez");
    expect(document.querySelector(".table-order-bridge button.secondary")?.getAttribute("aria-describedby")).toBe("table-order-bridge-note");
    expect(document.querySelector("#table-order-bridge-note")?.textContent).toContain("'İptal / iade' düğmesiyle hesabı iptal edin");
    expect(button("İptal / iade")).toBeDefined();
    expect(button("Öde")).toBeDefined();
    expect(onStartOrder).not.toHaveBeenCalled();
  });

  it("tells a cashier to contact an authorised manager when a billed table has a served line, instead of pointing at a cancel that would be refused", async () => {
    stubFetch(undefined, { ...tableOrder, items: [{ itemId: "k2", productName: "Pilav", quantity: 1, unitPrice: 20, totalPrice: 20, status: "Active", kitchenState: "Served" }] });
    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1 } });
    await flush();

    const note = document.querySelector("#table-order-bridge-note")?.textContent;
    expect(note).toContain("servis edilmiş ürün var");
    expect(note).toContain("Yetkili yöneticiye başvurun");
    expect(note).not.toContain("hesabı iptal edin");
  });

  it("keeps the cancel hint for a manager who may cancel a bill with served lines", async () => {
    stubFetch(undefined, { ...tableOrder, items: [{ itemId: "k2", productName: "Pilav", quantity: 1, unitPrice: 20, totalPrice: 20, status: "Active", kitchenState: "Served" }] });
    render({ canCancelServed: true, table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1 } });
    await flush();

    expect(document.querySelector("#table-order-bridge-note")?.textContent).toContain("hesabı iptal edin");
  });

  it("keeps the cancel hint for a cashier when no line of the billed table is served", async () => {
    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1 } });
    await flush();

    expect(document.querySelector("#table-order-bridge-note")?.textContent).toContain("hesabı iptal edin");
    expect(document.querySelector("#table-order-bridge-note")?.textContent).not.toContain("Yetkili yöneticiye");
  });

  it("does not point to a button the cashier does not have", () => {
    render({ canCollect: false, table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1 } });

    expect(document.querySelector("#table-order-bridge-note")?.textContent).toBe("Hesap açıldığı için ürün eklenemez.");
    expect(button("İptal / iade")).toBeUndefined();
  });

  it("keeps the continue button open on a table with an order and no bill", () => {
    render();

    expect(button("Ürün ekle")?.disabled).toBe(false);
  });

  it("offers no collection for an empty table", () => {
    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: null, currentBillId: null, rowVersion: 1 } });

    expect(button("Öde")).toBeUndefined();
    expect(button("Sipariş al")).toBeDefined();
  });

  it("gives a table that asked for its check a Öde button for the sent check and no wrong promise", async () => {
    const fetchMock = stubFetch(() => reply(200, { billId: "b-ask" }));
    const { navigateTo } = render({
      orderAllowed: false,
      table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: null, currentBillId: null, rowVersion: 1 },
      awaitingCheck: { orderId: "o-sent", itemCount: 3, total: 150 },
    });
    const text = document.querySelector(".table-order-bridge")!.textContent;

    expect(text).toContain("Garson hesabı kasaya gönderdi");
    expect(text).not.toContain("Sipariş al deyince");
    expect(text).not.toContain("uygun değil");
    expect(text).not.toContain("Bu masada açık hesap yok");
    await click("Öde");

    expect(String(otherCalls(fetchMock)[0][0])).toContain("/from-order/o-sent");
    expect(navigateTo).toHaveBeenCalledWith("/cashier/payments/split-payment/index.html?billId=b-ask");
  });

  it("makes Öde the only filled button on a table with a sent check, and says nothing about it without the bill permission", () => {
    const empty = { tableId: "tb-4", tableNumber: "T-4", currentOrderId: null, currentBillId: null, rowVersion: 1 };
    render({ table: empty, awaitingCheck: { orderId: "o-sent", itemCount: 3, total: 150 } });
    expect([...document.querySelectorAll(".table-order-bridge button.primary")].map((b) => b.textContent)).toEqual(["Öde"]);

    act(() => root!.unmount());
    render({ table: empty, canCollect: false, awaitingCheck: { orderId: "o-sent", itemCount: 3, total: 150 } });
    expect(button("Öde")).toBeUndefined();
    expect(document.querySelector(".table-order-bridge")?.textContent).not.toContain("Garson hesabı kasaya gönderdi");
    expect(document.querySelector(".table-order-bridge")?.textContent).not.toContain("3 kalem");
  });

  it("keeps the order's own check over the sent check when the table still holds an order", () => {
    render({ awaitingCheck: { orderId: "o-sent", itemCount: 3, total: 150 } });

    expect(document.querySelector(".table-order-bridge")?.textContent).not.toContain("Garson hesabı kasaya gönderdi");
  });

  it("offers no collection without the permission to open a bill", () => {
    render({ canCollect: false });

    expect(button("Öde")).toBeUndefined();
  });

  it("shows the error and the reason a refused table gives", () => {
    render({ orderAllowed: false, error: "Hesap açılamadı." });

    expect(document.body.textContent).toContain("sipariş almaya uygun değil");
    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Hesap açılamadı.");
  });
  it("names the table and lists the open bill's lines and total, leaving cancelled lines out", async () => {
    render();
    await flush();

    const panel = document.querySelector(".table-order-bridge")!;
    expect(panel.querySelector("h4")).toBeNull();
    expect(panel.textContent).toContain("Çay");
    expect(panel.textContent).toContain("Köfte");
    expect(panel.textContent).not.toContain("Yanlış ürün");
    expect(panel.querySelector(".table-order-bridge__total")?.textContent).toContain("150");
  });

  it("reads no bill for an empty table and offers Sipariş al without Öde", async () => {
    const fetchMock = stubFetch();
    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: null, currentBillId: null, rowVersion: 1 } });
    await flush();

    expect(fetchMock).not.toHaveBeenCalled();
    expect(button("Sipariş al")).toBeDefined();
    expect(button("Öde")).toBeUndefined();
    expect(document.querySelector(".table-order-bridge")?.textContent).toContain("Bu masada açık hesap yok");
  });

  it("makes Öde the only filled button, with Ürün ekle and the other actions secondary", async () => {
    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1 } });
    await flush();

    expect([...document.querySelectorAll(".table-order-bridge button.primary")].map((b) => b.textContent)).toEqual(["Öde"]);
    for (const label of ["Ürün ekle", "Böl", "Masa taşı", "İptal / iade"]) expect(button(label)?.classList.contains("primary")).toBe(false);
  });

  it("says a failed bill read could not be loaded instead of drawing an empty bill", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => reply(500, { error: { code: "X" } })));
    render();
    await flush();

    const panel = document.querySelector(".table-order-bridge")!;
    expect(panel.textContent).toContain("Hesap kalemleri alınamadı");
    expect(panel.querySelector(".table-order-bridge__total")).toBeNull();
  });

  it("Böl opens the split screen on the table's bill or order, and İptal / iade opens the payment screen with the refund section open", async () => {
    const withBill = render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1 } });
    await click("Böl");
    expect(withBill.navigateTo).toHaveBeenCalledWith("/billing?billId=b-1");
    await click("İptal / iade");
    expect(withBill.navigateTo).toHaveBeenLastCalledWith("/cashier/payments/split-payment/index.html?billId=b-1&open=refund");

    const withoutBill = render();
    await click("Böl");
    expect(withoutBill.navigateTo).toHaveBeenCalledWith("/billing?orderId=o-1");
    expect(button("İptal / iade")).toBeUndefined();
  });

  it("reads the lines again when the table's order changes, and not at all without the order permission", async () => {
    const fetchMock = stubFetch();
    render();
    await flush();
    const reads = () => fetchMock.mock.calls.filter(([url]) => String(url).includes("/orders/table/")).length;
    expect(reads()).toBe(1);
    await act(async () => root!.render(createElement(TableOrderBridge, {
      terminalId: "t-1", table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: null, rowVersion: 2, currentOrderTotal: 200 },
      orderAllowed: true, canManage: true, canCollect: true, canReadLines: true, canAddWithoutKitchen: true, busy: false, onStartOrder: vi.fn(), onAddWithoutKitchen: vi.fn(), onSent: vi.fn(),
    })));
    await flush();
    expect(reads()).toBe(2);

    const blocked = stubFetch();
    render({ canReadLines: false });
    await flush();
    expect(blocked).not.toHaveBeenCalled();
    expect(document.querySelector(".table-order-bridge")?.textContent).not.toContain("alınamadı");
  });

  const draftOrder = { ...tableOrder, status: "Draft", items: [
    { itemId: "i1", productName: "Çay", quantity: 2, unitPrice: 25, totalPrice: 50, status: "Draft", kitchenState: "NotSent" },
    { itemId: "i2", productName: "Su", quantity: 1, unitPrice: 10, totalPrice: 10, status: "Draft", kitchenState: "NotSent" },
    { itemId: "i3", productName: "Köfte", quantity: 1, unitPrice: 100, totalPrice: 100, status: "Active", kitchenState: "Sent" },
    { itemId: "i4", productName: "Kolsuz", quantity: 1, unitPrice: 5, totalPrice: 5, status: "Active", kitchenState: "NotSent" },
  ] };

  it("counts only the lines not yet sent on Mutfağa gönder and sends the table's order to the kitchen", async () => {
    const fetchMock = stubFetch(() => reply(200, { orderId: "o-1", orderNumber: "1", revision: 9, rowVersion: 10 }), draftOrder);
    const { onSent } = render();
    await flush();

    expect(button("Mutfağa gönder (3)")).toBeDefined();
    expect([...document.querySelectorAll(".table-order-bridge__lines li")].map((li) => li.textContent?.includes("Mutfağa gönderilmedi"))).toEqual([true, true, false, false]);
    expect(document.querySelector("#table-order-bridge-note")?.textContent).toContain("Gönderilmemiş ürünler var");
    expect(button("Mutfağa göndermeden hesaba ekle")).toBeUndefined();
    await click("Mutfağa gönder (3)");

    const calls = otherCalls(fetchMock).map(([url, init]) => `${init?.method} ${String(url).replace(/^.*terminals\/[^/]+/, "")}`);
    expect(calls).toEqual(["POST /orders/table", "POST /orders/o-1/submit"]);
    expect(JSON.parse(String(otherCalls(fetchMock)[1][1]!.body))).toMatchObject({ expectedRevision: 9 });
    expect(onSent).toHaveBeenCalledTimes(1);
  });

  it("reads the bill lines again after the send, so no line stays marked as not sent", async () => {
    const sentOrder = { ...draftOrder, status: "Submitted", items: draftOrder.items.map((item) => ({ ...item, status: "Active", kitchenState: "Sent" })) };
    let submitted = false;
    vi.stubGlobal("fetch", vi.fn(async (url: string) => {
      const path = String(url);
      if (path.includes("/orders/table/")) return reply(200, submitted ? sentOrder : draftOrder);
      if (path.endsWith("/submit")) submitted = true;
      return reply(200, { orderId: "o-1", orderNumber: "1", revision: 9, rowVersion: 10 });
    }));
    render();
    await flush();
    await click("Mutfağa gönder (3)");
    await flush();

    expect(document.querySelector(".table-order-bridge__lines")?.textContent).not.toContain("Mutfağa gönderilmedi");
    expect([...document.querySelectorAll("button")].some((b) => b.textContent?.startsWith("Mutfağa gönder ("))).toBe(false);
  });

  it("marks a line the cashier added at the till without a kitchen ticket", async () => {
    stubFetch(undefined, { ...tableOrder, items: [
      tableOrder.items[0],
      { itemId: "i4", productName: "Simit", quantity: 1, unitPrice: 25, totalPrice: 25, status: "Active", kitchenState: "NotSent", addedWithoutKitchenReason: "waiter-forgot" },
    ] });
    render();
    await flush();

    expect([...document.querySelectorAll(".table-order-bridge__lines li")].map((li) => li.textContent?.includes("Kasada eklendi (mutfağa gitmedi)"))).toEqual([false, true]);
  });

  it("names an unsent line that was also added at the till only once", async () => {
    stubFetch(undefined, { ...tableOrder, status: "Draft", items: [
      { itemId: "i5", productName: "Simit", quantity: 1, unitPrice: 25, totalPrice: 25, status: "Draft", kitchenState: "NotSent", addedWithoutKitchenReason: "waiter-forgot" },
    ] });
    render();
    await flush();

    const line = document.querySelector(".table-order-bridge__lines li")!;
    expect(line.querySelectorAll("small")).toHaveLength(1);
    expect(line.textContent).toContain("Mutfağa gönderilmedi");
  });

  it("names a line the kitchen has marked ready or served, and leaves preparing and sent lines unmarked", async () => {
    stubFetch(undefined, { ...tableOrder, items: [
      { itemId: "k1", productName: "Çorba", quantity: 1, unitPrice: 20, totalPrice: 20, status: "Active", kitchenState: "Ready" },
      { itemId: "k2", productName: "Pilav", quantity: 1, unitPrice: 20, totalPrice: 20, status: "Active", kitchenState: "Served" },
      { itemId: "k3", productName: "Salata", quantity: 1, unitPrice: 20, totalPrice: 20, status: "Active", kitchenState: "Preparing" },
      { itemId: "k4", productName: "Ayran", quantity: 1, unitPrice: 20, totalPrice: 20, status: "Active", kitchenState: "Sent" },
    ] });
    render();
    await flush();

    expect([...document.querySelectorAll(".table-order-bridge__lines li small")].map((note) => note.textContent)).toEqual([" · Hazır", " · Servis edildi"]);
  });

  it("reads the lines again while the panel stays open, so a line the kitchen marks ready appears without reselecting the table", async () => {
    vi.useFakeTimers();
    let order: unknown = tableOrder;
    vi.stubGlobal("fetch", vi.fn(async () => reply(200, order)));
    render();
    await flush();
    expect(document.querySelectorAll(".table-order-bridge__lines li small")).toHaveLength(0);

    order = { ...tableOrder, items: [{ ...tableOrder.items[0], kitchenState: "Ready" }, tableOrder.items[1]] };
    await act(async () => { await vi.advanceTimersByTimeAsync(15_000); });

    expect([...document.querySelectorAll(".table-order-bridge__lines li small")].map((note) => note.textContent)).toEqual([" · Hazır"]);
  });

  it("stops re-reading the lines once the panel is closed", async () => {
    vi.useFakeTimers();
    const fetchMock = vi.fn(async () => reply(200, tableOrder));
    vi.stubGlobal("fetch", fetchMock);
    render();
    await flush();
    act(() => root!.unmount());
    root = null;
    const calls = fetchMock.mock.calls.length;
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000); });

    expect(fetchMock.mock.calls.length).toBe(calls);
  });

  it("keeps the lines on screen when a background re-read fails", async () => {
    vi.useFakeTimers();
    let fail = false;
    vi.stubGlobal("fetch", vi.fn(async () => (fail ? reply(500, {}) : reply(200, tableOrder))));
    render();
    await flush();
    fail = true;
    await act(async () => { await vi.advanceTimersByTimeAsync(15_000); });

    expect(document.querySelectorAll(".table-order-bridge__lines li")).toHaveLength(2);
    expect(document.querySelector(".table-order-bridge")?.textContent).not.toContain("alınamadı");
  });

  it("shows the server's refusal when sending fails and does not report the order as sent", async () => {
    stubFetch(() => reply(409, { error: { code: "STALE", message: "Sipariş başka yerde değişti." } }), draftOrder);
    const { onSent } = render();
    await flush();

    await click("Mutfağa gönder (3)");

    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Sipariş başka yerde değişti.");
    expect(onSent).not.toHaveBeenCalled();
  });

  it("offers Mutfağa göndermeden hesaba ekle only on a sent, unbilled order, and only with the permission", async () => {
    const { onAddWithoutKitchen } = render();
    await flush();
    expect(button("Mutfağa gönder (0)")).toBeUndefined();
    await click("Mutfağa göndermeden hesaba ekle");
    expect(onAddWithoutKitchen).toHaveBeenCalledTimes(1);

    render({ canAddWithoutKitchen: false });
    await flush();
    expect(button("Mutfağa göndermeden hesaba ekle")).toBeUndefined();

    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1 } });
    await flush();
    expect(button("Mutfağa göndermeden hesaba ekle")).toBeUndefined();
  });

  it("puts Böl and Masa taşı in one mini row under the main buttons, and Sipariş al is the only filled button of an empty table", async () => {
    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1 } });
    await flush();
    expect([...document.querySelectorAll(".table-order-bridge__mini button")].map((b) => b.textContent)).toEqual(["Böl", "Masa taşı"]);

    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: null, currentBillId: null, rowVersion: 1 } });
    await flush();
    expect([...document.querySelectorAll(".table-order-bridge button.primary")].map((b) => b.textContent)).toEqual(["Sipariş al"]);
    expect(document.querySelector(".table-order-bridge__mini")).toBeNull();
  });

  it("Masa taşı hands over to the table move form and is closed when the table cannot be moved", async () => {
    const movable = render();
    await click("Masa taşı");
    expect(movable.onTransfer).toHaveBeenCalledTimes(1);

    render({ onTransfer: undefined });
    expect(button("Masa taşı")?.disabled).toBe(true);
  });

  it("says why Masa taşı is closed when the table cannot be moved, and not when it can or another till holds the bill", () => {
    render({ onTransfer: undefined });
    expect(document.querySelector(".table-order-bridge__reason")?.textContent).toBe("Bu masa şu anda taşınamaz.");

    render();
    expect(document.querySelector(".table-order-bridge__reason")).toBeNull();

    render({ onTransfer: undefined, table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1, billOnOtherTerminal: true } });
    expect(document.querySelector(".table-order-bridge__reason")).toBeNull();
  });

  it("draws İptal / iade alone in its own destructive row, apart from Böl and Masa taşı", async () => {
    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1 } });
    await flush();

    const cancel = button("İptal / iade")!;
    expect(cancel.closest(".table-order-bridge__mini")).toBeNull();
    expect(cancel.parentElement?.className).toBe("table-order-bridge__danger");
    expect(cancel.className).toBe("table-order-bridge__cancel");
    expect(cancel.parentElement?.children).toHaveLength(1);
  });

  it("closes Öde, Böl, Masa taşı and İptal / iade and says why when another till holds the bill", () => {
    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1, billOnOtherTerminal: true } });

    for (const label of ["Öde", "Böl", "Masa taşı", "İptal / iade"]) expect(button(label)?.disabled).toBe(true);
    expect(button("Masa taşı")?.title).toContain("Bu hesap başka bir kasada açık");
    expect(document.body.textContent).toContain("Bu hesap başka bir kasada açık");
  });

  it("keeps Öde, Böl, Masa taşı and İptal / iade open for a bill this till may handle", () => {
    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1, billOnOtherTerminal: false } });

    for (const label of ["Öde", "Böl", "Masa taşı", "İptal / iade"]) expect(button(label)?.disabled).toBe(false);
    expect(document.body.textContent).not.toContain("Bu hesap başka bir kasada açık");
  });

  it("draws the action buttons above the bill lines, so a long bill never pushes them off a short screen", async () => {
    stubFetch(() => reply(200, {}));
    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1 } });
    await flush();

    const lines = document.querySelector(".table-order-bridge__lines")!;
    for (const selector of [".table-order-bridge__actions", ".table-order-bridge__mini"]) {
      expect(document.querySelector(selector)!.compareDocumentPosition(lines) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    }
  });

  it("draws Böl, Masa taşı and İptal / iade only for those who may use them", () => {
    render({ canCollect: false, canManage: false, onTransfer: undefined });

    for (const label of ["Öde", "Böl", "Masa taşı", "İptal / iade"]) expect(button(label)).toBeUndefined();
  });

  it("drops the lines and the order buttons of a table that is freed while the panel stays open", async () => {
    render();
    await flush();
    expect(button("Mutfağa göndermeden hesaba ekle")).toBeDefined();

    act(() => root!.render(createElement(TableOrderBridge, {
      terminalId: "t-1", table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: null, currentBillId: null, rowVersion: 2 },
      orderAllowed: true, canManage: true, canCollect: true, canReadLines: true, canAddWithoutKitchen: true, busy: false,
      onStartOrder: vi.fn(), onAddWithoutKitchen: vi.fn(), onSent: vi.fn(), onTransfer: vi.fn(), navigateTo: vi.fn(),
    })));
    await flush();

    expect(button("Mutfağa göndermeden hesaba ekle")).toBeUndefined();
    expect(button("Sipariş al")).toBeDefined();
    expect(document.querySelector(".table-order-bridge__lines")).toBeNull();
    expect(document.body.textContent).toContain("Bu masada açık hesap yok.");
  });

  const rerender = (table: Parameters<typeof TableOrderBridge>[0]["table"], props: Partial<Parameters<typeof TableOrderBridge>[0]> = {}) => act(() => root!.render(createElement(TableOrderBridge, {
    terminalId: "t-1", table, orderAllowed: true, canManage: true, canCollect: true, canReadLines: true, canAddWithoutKitchen: true, busy: false,
    onStartOrder: vi.fn(), onAddWithoutKitchen: vi.fn(), onSent: vi.fn(), onTransfer: vi.fn(), navigateTo: vi.fn(), ...props,
  })));

  it("shows nothing of the previous order, and no send button, until the table's new order is read", async () => {
    stubFetch(() => reply(500, {}), draftOrder);
    render();
    await flush();
    expect(button("Mutfağa gönder (3)")).toBeDefined();

    rerender({ tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-2", currentBillId: null, rowVersion: 2 });

    expect(button("Mutfağa gönder (3)")).toBeUndefined();
    expect(document.querySelector(".table-order-bridge__lines")).toBeNull();
  });

  it("offers no order-line buttons once the lines may no longer be read", async () => {
    render();
    await flush();
    expect(button("Mutfağa göndermeden hesaba ekle")).toBeDefined();

    rerender({ tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: null, rowVersion: 1 }, { canReadLines: false });

    expect(button("Mutfağa göndermeden hesaba ekle")).toBeUndefined();
  });

  it("does not report a failed bill read for a table that has a bill but no order to read", async () => {
    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: null, currentBillId: "b-1", rowVersion: 1 } });
    await flush();

    expect(document.body.textContent).not.toContain("Hesap kalemleri alınamadı");
  });

  describe("panel style rules from docs/CASHIER_UI_RULES.md", () => {
    const stylesheet = readFileSync(join(import.meta.dirname, "../styles.css"), "utf8");
    const bridgeRules = [...stylesheet.replace(/\/\*[\s\S]*?\*\//g, "").matchAll(/^([^@{}\n][^{}]*)\{([^{}]*)\}/gm)]
      .map((match) => ({ selector: match[1].replace(/\s+/g, " ").trim(), body: match[2] }))
      .filter((rule) => rule.selector.includes("table-order-bridge"));
    const body = (selector: string) => bridgeRules.filter((rule) => rule.selector === selector).map((rule) => rule.body).join(";");

    it.each([".table-order-bridge", ".table-order-bridge__actions", ".table-order-bridge__mini"])("keeps at least 16px between the buttons of %s", (selector) => {
      const declared = [...body(selector).matchAll(/(?:^|[;\s])(?:row-|column-)?gap:\s*([^;]+)/g)].flatMap((match) => match[1].trim().split(/\s+/));
      expect(declared.length).toBeGreaterThan(0);
      // Only rem and px are read; any other unit fails here instead of passing as a number.
      for (const value of declared) expect(/^[\d.]+rem$/.test(value) ? Number.parseFloat(value) * 16 : /^[\d.]+px$/.test(value) ? Number.parseFloat(value) : Number.NaN, value).toBeGreaterThanOrEqual(16);
    });

    it("sets every panel text at 14px or larger", () => {
      const sizes = bridgeRules.flatMap((rule) => [...rule.body.matchAll(/font-size:\s*([\d.]+)(rem|px)/g)].map((match) => Number(match[1]) * (match[2] === "rem" ? 16 : 1)));
      expect(sizes.length).toBeGreaterThan(5);
      for (const selector of [".table-order-bridge p", ".table-order-bridge__reason", ".table-order-bridge__mini button", ".table-order-bridge__lines li, .table-order-bridge__total"]) expect(body(selector), selector).toMatch(/font-size:/);
      expect(sizes.filter((size) => size < 14)).toEqual([]);
    });

    it("answers a touch on İptal / iade and keeps it readable while closed", () => {
      expect(body(".table-order-bridge__cancel:hover:not(:disabled)")).toContain("background: var(--ds-color-danger-soft)");
      expect(body(".table-order-bridge__cancel:disabled")).toMatch(/opacity:\s*1;/);
      expect(body(".table-order-bridge__cancel:disabled")).toContain("color: var(--ds-color-muted)");
    });

    it("draws İptal / iade with the danger tokens and the panel with design tokens only", () => {
      expect(body(".table-order-bridge__cancel")).toMatch(/color:\s*var\(--ds-color-danger\)/);
      expect(body(".table-order-bridge__cancel")).toMatch(/border:\s*1px solid var\(--ds-color-danger\)/);
      expect(bridgeRules.map((rule) => rule.body).join(";")).not.toMatch(/#e2cfc8|#fffaf7|#795b18|#922c36|#fff0f1/);
    });
  });
});

describe("TableOrderBridge mutfaktaki ürünü iptal et", () => {
  const sentOrder = {
    ...tableOrder,
    items: [
      { itemId: "i1", productName: "Çay", quantity: 2, unitPrice: 25, totalPrice: 50, status: "Active", kitchenState: "Sent", canVoidSent: true },
      { itemId: "i2", productName: "Köfte", quantity: 1, unitPrice: 100, totalPrice: 100, status: "Active", kitchenState: "Served", canVoidSent: false },
      { itemId: "i4", productName: "Ayran", quantity: 1, unitPrice: 20, totalPrice: 20, status: "Draft", kitchenState: "NotSent" },
    ],
  };
  const action = "Mutfaktaki ürünü iptal et";
  const flush = () => act(async () => { await Promise.resolve(); });
  const press = (target: Element | undefined) => act(async () => { target!.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
  const reasonButton = (label: string) => [...document.querySelectorAll<HTMLButtonElement>(".table-order-bridge__void button")].find((b) => b.textContent === label);
  const confirmButton = () => document.querySelector<HTMLButtonElement>(".table-order-bridge__void-confirm");
  const voidCalls = (mock: ReturnType<typeof stubFetch>) => mock.mock.calls.filter(([url]) => String(url).includes("/void-sent"));
  const chooseAndConfirm = async (label: string) => {
    await press(button(action));
    await press(reasonButton(label));
    await press(confirmButton()!);
    await flush();
  };

  it("shows the line's note under it on the table panel, and none for a line without a note", async () => {
    stubFetch(undefined, { ...sentOrder, items: sentOrder.items.map((item) => item.itemId === "i1" ? { ...item, specialInstructions: "az pişmiş" } : item) });
    render();
    await flush();

    const lines = [...document.querySelectorAll(".table-order-bridge__lines li")];
    expect(lines[0].querySelector(".table-order-bridge__line-note")?.textContent).toBe("Not: az pişmiş");
    expect(document.querySelectorAll(".table-order-bridge__line-note")).toHaveLength(1);
  });

  it("offers the action only on a line the server marks voidable, once per line", async () => {
    stubFetch(undefined, sentOrder);
    render();
    await flush();

    expect([...document.querySelectorAll(".table-order-bridge__lines li")].map((li) => li.textContent?.includes(action))).toEqual([true, false, false]);
  });

  it("names the line in the action's accessible name, so a screen reader can tell the lines apart", async () => {
    stubFetch(undefined, sentOrder);
    render();
    await flush();

    expect(button(action)?.getAttribute("aria-label")).toBe(`${action}: Çay`);
  });

  it("offers it to nobody who may not read the lines, and closes it while another till holds the bill or the screen is busy", async () => {
    stubFetch(undefined, sentOrder);
    render({ canReadLines: false });
    await flush();
    expect(button(action)).toBeUndefined();

    render({ busy: true });
    await flush();
    expect(button(action)?.disabled).toBe(true);

    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1, billOnOtherTerminal: true } });
    await flush();
    expect(button(action)?.disabled).toBe(true);
  });

  it("sends nothing on the first touch and lists four reasons with none chosen and the confirm button closed with its reason", async () => {
    const mock = stubFetch(undefined, sentOrder);
    render();
    await flush();
    await press(button(action));

    expect(voidCalls(mock)).toHaveLength(0);
    expect([...document.querySelectorAll(".table-order-bridge__void button[aria-pressed]")].map((b) => [b.textContent, b.getAttribute("aria-pressed")])).toEqual([
      ["Müşteri vazgeçti", "false"], ["Yanlış girdim", "false"], ["Ürün kalmadı", "false"], ["İki kez girilmiş", "false"],
    ]);
    expect(confirmButton()?.disabled).toBe(true);
    expect(document.querySelector(".table-order-bridge__void")?.textContent).toContain("Gerekçe seçin");
  });

  it("names the table, the line and its amount on the confirm button once a reason is chosen", async () => {
    stubFetch(undefined, sentOrder);
    render();
    await flush();
    await press(button(action));
    await press(reasonButton("Yanlış girdim"));

    expect(confirmButton()?.disabled).toBe(false);
    expect(confirmButton()?.textContent).toBe(`T-4 masasındaki 2 × Çay (${formatMoney(50)}) ürününü iptal et`);
    expect(reasonButton("Yanlış girdim")?.getAttribute("aria-pressed")).toBe("true");
  });

  it("sends the order's own row version, the reason and a key, then reads the lines again and tells the table list", async () => {
    const mock = stubFetch(() => reply(200, { status: "Applied", stockRestored: false }), sentOrder);
    const { onSent } = render();
    await flush();
    await chooseAndConfirm("Ürün kalmadı");

    const [url, init] = voidCalls(mock)[0];
    expect(url).toBe("/api/v1/terminals/t-1/orders/o-1/items/i1/void-sent");
    expect(JSON.parse(String(init!.body))).toEqual({ idempotencyKey: expect.any(String), expectedRowVersion: 3, reasonCode: "ProductUnavailable" });
    expect(onSent).toHaveBeenCalledTimes(1);
    expect(document.querySelector(".table-order-bridge__void")).toBeNull();
    expect(document.querySelector("[role=status]")?.textContent).toBe("Ürün iptal edildi.");
  });

  it("says the stock went back when the server reports it", async () => {
    stubFetch(() => reply(200, { status: "Applied", stockRestored: true }), sentOrder);
    render();
    await flush();
    await chooseAndConfirm("Müşteri vazgeçti");

    expect(document.querySelector("[role=status]")?.textContent).toBe("Ürün iptal edildi, stok geri alındı.");
  });

  describe("when the void waits for a manager", () => {
    const type = (input: HTMLInputElement, value: string) => act(async () => {
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(input, value);
      input.dispatchEvent(new Event("input", { bubbles: true }));
    });
    const field = (label: string) => [...document.querySelectorAll("label")].find((node) => node.textContent === label)!.querySelector("input")!;
    const approveCalls = (mock: ReturnType<typeof vi.fn>) => mock.mock.calls.filter(([url]) => String(url).includes("/approvals/"));
    const stubWaiting = (approve: () => Response) => {
      const mock = vi.fn(async (url: string) => {
        if (String(url).includes("/approvals/")) return approve();
        if (String(url).includes("/void-sent")) return voidAnswers.shift()!();
        return reply(200, sentOrder);
      });
      vi.stubGlobal("fetch", mock);
      return mock;
    };
    let voidAnswers: Array<() => Response> = [];
    const fillAndApprove = async () => {
      await type(field("Yönetici kullanıcı adı"), "mudur1");
      await type(field("Yönetici parolası"), "Mudur-Sifre-1");
      await press(button("Onayla ve iptal et"));
      await flush();
    };

    it("opens the manager's name and password on the same line and does not report the line as voided", async () => {
      voidAnswers = [() => reply(202, { status: "Pending", grantId: "g-1" })];
      stubWaiting(() => reply(200, {}));
      const { onSent } = render();
      await flush();
      await chooseAndConfirm("Yanlış girdim");

      expect(onSent).not.toHaveBeenCalled();
      expect(document.querySelector(".table-order-bridge__lines li")?.textContent).toContain("Bu iptal için yönetici onayı gerekir");
      expect(field("Yönetici kullanıcı adı").maxLength).toBe(64);
      expect(field("Yönetici parolası").type).toBe("password");
      expect(field("Yönetici parolası").maxLength).toBe(256);
      expect(button("Onayla ve iptal et")?.disabled).toBe(true);
      expect(field("Yönetici kullanıcı adı").getAttribute("autocapitalize")).toBe("none");
      expect(field("Yönetici kullanıcı adı").getAttribute("spellcheck")).toBe("false");
    });

    it("approves under the waiting request's own key, then asks for the void again under that key and reports it applied", async () => {
      voidAnswers = [() => reply(202, { status: "Pending", grantId: "g-1" }), () => reply(200, { status: "Applied", stockRestored: false })];
      const mock = stubWaiting(() => reply(200, { grantId: "g-1" }));
      const { onSent } = render();
      await flush();
      await chooseAndConfirm("Yanlış girdim");
      await fillAndApprove();

      const [approveUrl, approveInit] = approveCalls(mock)[0];
      expect(approveUrl).toBe("/api/v1/terminals/t-1/orders/o-1/items/i1/approvals/g-1/approve");
      const keys = voidCalls(mock).map(([, init]) => (JSON.parse(String(init!.body)) as { idempotencyKey: string }).idempotencyKey);
      expect(JSON.parse(String(approveInit!.body))).toEqual({ idempotencyKey: keys[0], username: "mudur1", password: "Mudur-Sifre-1" });
      expect(keys).toHaveLength(2);
      expect(keys[1]).toBe(keys[0]);
      expect(onSent).toHaveBeenCalledTimes(1);
      expect(document.querySelector("[role=status]")?.textContent).toBe("Ürün iptal edildi.");
      expect(document.querySelector("input[type=password]")).toBeNull();
    });

    it("shows the server's refusal, empties the password and keeps the name and the fields open", async () => {
      voidAnswers = [() => reply(202, { status: "Pending", grantId: "g-1" })];
      const mock = stubWaiting(() => reply(403, { error: { code: "MANAGER_CREDENTIAL_INVALID", message: "Yönetici adı veya şifre hatalı." } }));
      const { onSent } = render();
      await flush();
      await chooseAndConfirm("Yanlış girdim");
      await fillAndApprove();

      expect(document.querySelector("[role=alert]")?.textContent).toBe("Yönetici adı veya şifre hatalı.");
      expect(field("Yönetici parolası").value).toBe("");
      expect(field("Yönetici kullanıcı adı").value).toBe("mudur1");
      expect(voidCalls(mock)).toHaveLength(1);
      expect(onSent).not.toHaveBeenCalled();
    });

    it("Vazgeç closes the manager fields without sending anything more", async () => {
      voidAnswers = [() => reply(202, { status: "Pending", grantId: "g-1" })];
      const mock = stubWaiting(() => reply(200, {}));
      render();
      await flush();
      await chooseAndConfirm("Yanlış girdim");
      await press(button("Vazgeç"));

      expect(document.querySelector("input[type=password]")).toBeNull();
      expect(approveCalls(mock)).toHaveLength(0);
      expect(button(action)).toBeDefined();
    });
  });

  it("shows the server's Turkish refusal and keeps the line and the table list as they were", async () => {
    stubFetch(() => reply(403, { error: { code: "GRANT_DENIED", message: "İptal talebi reddedildi." } }), sentOrder);
    const { onSent } = render();
    await flush();
    await chooseAndConfirm("Yanlış girdim");

    expect(document.querySelector("[role=alert]")?.textContent).toBe("İptal talebi reddedildi.");
    expect(onSent).not.toHaveBeenCalled();
    expect(document.querySelector(".table-order-bridge__lines")?.textContent).toContain("Çay");
  });

  it("reads the lines again after a conflict, so a line served meanwhile loses its action", async () => {
    let reads = 0;
    const served = { ...sentOrder, items: sentOrder.items.map((item) => item.itemId === "i1" ? { ...item, kitchenState: "Served", canVoidSent: false } : item) };
    vi.stubGlobal("fetch", vi.fn(async (url: string, init?: RequestInit) => {
      if (String(url).includes("/void-sent")) return reply(409, { error: { code: "ITEM_ALREADY_SERVED", message: "Ürün zaten servis edildi." } });
      if (String(url).includes("/orders/table/") && init?.method !== "POST") return reply(200, ++reads === 1 ? sentOrder : served);
      return reply(500, {});
    }));
    render();
    await flush();
    await chooseAndConfirm("Yanlış girdim");
    await flush();

    expect(document.querySelector("[role=alert]")?.textContent).toBe("Ürün zaten servis edildi.");
    expect(button(action)).toBeUndefined();
  });

  it("Vazgeç closes the reasons without sending anything", async () => {
    const mock = stubFetch(undefined, sentOrder);
    render();
    await flush();
    await press(button(action));
    await press(button("Vazgeç"));

    expect(document.querySelector(".table-order-bridge__void")).toBeNull();
    expect(voidCalls(mock)).toHaveLength(0);
  });
});
