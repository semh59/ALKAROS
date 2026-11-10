// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { registerSessionExpiredHandler } from "../api";
import { TableOrderBridge } from "./TableOrderBridge";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root | null = null;
afterEach(() => {
  vi.unstubAllGlobals();
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
});

const render = (props: Partial<Parameters<typeof TableOrderBridge>[0]> = {}) => {
  const onStartOrder = vi.fn();
  const navigateTo = vi.fn();
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  act(() => root!.render(createElement(TableOrderBridge, {
    terminalId: "t-1", table: { tableNumber: "T-4", currentOrderId: "o-1", currentBillId: null },
    orderAllowed: true, canManage: true, canCollect: true, busy: false, onStartOrder, navigateTo, ...props,
  })));
  return { onStartOrder, navigateTo };
};
const button = (label: string) => [...document.querySelectorAll("button")].find((b) => b.textContent === label);

describe("TableOrderBridge", () => {
  const click = (label: string) => act(async () => { button(label)!.dispatchEvent(new MouseEvent("click", { bubbles: true })); });

  it("Hesabı al opens the order's bill and goes to the payment page", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({ billId: "b-new" }), { status: 200, headers: { "content-type": "application/json" } })));
    const { navigateTo } = render();

    await click("Hesabı al");

    expect(navigateTo).toHaveBeenCalledWith("/cashier/payments/split-payment/index.html?billId=b-new");
  });

  it("Hesabı al goes straight to a bill the table already has", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    const { navigateTo } = render({ table: { tableNumber: "T-4", currentOrderId: null, currentBillId: "b-1" } });

    await click("Hesabı al");

    expect(navigateTo).toHaveBeenCalledWith("/cashier/payments/split-payment/index.html?billId=b-1");
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("a refused bill opening shows a Turkish message and stays on the screen", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({ error: { code: "ORDER_NOT_SUBMITTED" } }), { status: 409, headers: { "content-type": "application/json" } })));
    const { navigateTo } = render();

    await click("Hesabı al");

    expect(navigateTo).not.toHaveBeenCalled();
    expect(document.querySelector('[role="alert"]')?.textContent?.length).toBeGreaterThan(5);
  });

  it("a session that ended while opening the bill is reported to the session handler", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({ error: { code: "UNAUTHORIZED" } }), { status: 401, headers: { "content-type": "application/json" } })));
    const ended = vi.fn();
    registerSessionExpiredHandler(ended);
    render();

    await click("Hesabı al");
    registerSessionExpiredHandler(null);

    expect(ended).toHaveBeenCalledTimes(1);
  });

  it("offers no collection for an empty table", () => {
    render({ table: { tableNumber: "T-4", currentOrderId: null, currentBillId: null } });

    expect(button("Hesabı al")).toBeUndefined();
    expect(button("Masada sipariş aç")).toBeDefined();
  });

  it("offers no collection without the permission to open a bill", () => {
    render({ canCollect: false });

    expect(button("Hesabı al")).toBeUndefined();
  });

  it("shows the error and the reason a refused table gives", () => {
    render({ orderAllowed: false, error: "Hesap açılamadı." });

    expect(document.body.textContent).toContain("sipariş almaya uygun değil");
    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Hesap açılamadı.");
  });
});
