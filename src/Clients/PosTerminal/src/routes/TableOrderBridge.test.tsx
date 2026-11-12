// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { registerSessionExpiredHandler } from "../api";
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

  it("keeps the continue button open on a table with an order and no bill", () => {
    render();

    expect(button("Ürün ekle")?.disabled).toBe(false);
  });

  it("offers no collection for an empty table", () => {
    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: null, currentBillId: null, rowVersion: 1 } });

    expect(button("Öde")).toBeUndefined();
    expect(button("Sipariş al")).toBeDefined();
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

  it("Böl and İptal / iade open the billing screen on the table's bill, or on its order when it has no bill yet", async () => {
    const withBill = render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1 } });
    await click("Böl");
    expect(withBill.navigateTo).toHaveBeenCalledWith("/billing?billId=b-1");
    await click("İptal / iade");
    expect(withBill.navigateTo).toHaveBeenLastCalledWith("/billing?billId=b-1");

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

  it("puts Böl, Masa taşı and İptal / iade in one mini row under the main buttons, and Sipariş al is the only filled button of an empty table", async () => {
    render({ table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: "b-1", rowVersion: 1 } });
    await flush();
    expect([...document.querySelectorAll(".table-order-bridge__mini button")].map((b) => b.textContent)).toEqual(["Böl", "Masa taşı", "İptal / iade"]);

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

  it("draws Böl, Masa taşı and İptal / iade only for those who may use them", () => {
    render({ canCollect: false, canManage: false, onTransfer: undefined });

    for (const label of ["Öde", "Böl", "Masa taşı", "İptal / iade"]) expect(button(label)).toBeUndefined();
  });
});
