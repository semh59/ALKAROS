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
    { itemId: "i1", productName: "Çay", quantity: 2, unitPrice: 25, totalPrice: 50, status: "Active" },
    { itemId: "i2", productName: "Köfte", quantity: 1, unitPrice: 100, totalPrice: 100, status: "Active" },
    { itemId: "i3", productName: "Yanlış ürün", quantity: 1, unitPrice: 9, totalPrice: 9, status: "Cancelled" },
  ],
};
const stubFetch = (other: () => Response = () => reply(500, {})) => {
  const mock = vi.fn(async (url: string) => String(url).includes("/orders/table/") ? reply(200, tableOrder) : other());
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
  const onTransfer = vi.fn();
  const navigateTo = vi.fn();
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  act(() => root!.render(createElement(TableOrderBridge, {
    terminalId: "t-1", table: { tableId: "tb-4", tableNumber: "T-4", currentOrderId: "o-1", currentBillId: null, rowVersion: 1 },
    orderAllowed: true, canManage: true, canCollect: true, canReadLines: true, busy: false, onStartOrder, onTransfer, navigateTo, ...props,
  })));
  return { onStartOrder, onTransfer, navigateTo };
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
    expect(document.querySelector(".table-order-bridge")?.textContent).toContain("Bu masadaki hesabı alın.");
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
    expect(document.querySelector(".table-order-bridge")?.textContent).toContain("Hesabı iptal et");
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
    expect(panel.querySelector("h4")?.textContent).toBe("T-4");
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
      orderAllowed: true, canManage: true, canCollect: true, canReadLines: true, busy: false, onStartOrder: vi.fn(),
    })));
    await flush();
    expect(reads()).toBe(2);

    const blocked = stubFetch();
    render({ canReadLines: false });
    await flush();
    expect(blocked).not.toHaveBeenCalled();
    expect(document.querySelector(".table-order-bridge")?.textContent).not.toContain("alınamadı");
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
