// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { NfcOrder } from "./NfcOrder";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

const CATALOG = [
  { productId: "p1", sku: "corba", name: "Çorba", categoryCode: "SOUP", categoryName: "Çorba", unitPrice: 60, taxRate: 10 },
  { productId: "p2", sku: "kofte", name: "Köfte", categoryCode: "MAIN", categoryName: "Ana Yemek", unitPrice: 280, taxRate: 10 },
];

/**
 * V12-NFC-003: the customer-facing NFC order page (`/nfc/{tableId}`). No
 * login of any kind — every fetch here is anonymous, matching the real
 * `V12-NFC-001` API it calls.
 */
describe("NfcOrder", () => {
  let root: Root | null = null;

  async function render(path: string) {
    window.history.pushState(null, "", path);
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(<NfcOrder /> as ReactElement));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("with an invalid link (no table id), shows a clear error instead of a broken menu", async () => {
    await render("/nfc/");

    expect(document.body.textContent).toContain("Bu bağlantı geçersiz");
  });

  it("loads and renders the menu for a valid table link", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse(CATALOG)));

    await render("/nfc/table-1");
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Çorba");
    expect(document.body.textContent).toContain("Köfte");
  });

  it("adding items shows the cart bar with the running total, and placing the order shows the confirmation screen", async () => {
    const placedOrder = {
      orderId: "o1",
      tableId: "table-1",
      tableNumber: "T-01",
      status: "Accepted",
      rowVersion: 3,
      totalAmount: 60,
      items: [
        { itemId: "i1", productId: "p1", productName: "Çorba", quantity: 1, unitPrice: 60, totalPrice: 60, specialInstructions: null },
      ],
      createdAt: new Date().toISOString(),
    };
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/catalog")) return jsonResponse(CATALOG);
      if (path.includes("/orders")) return jsonResponse(placedOrder);
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render("/nfc/table-1");
    await act(async () => Promise.resolve());

    const increaseCorba = document.querySelector<HTMLButtonElement>('[aria-label="Çorba adedini artır"]')!;
    await act(async () => increaseCorba.click());

    expect(document.body.textContent).toContain("Siparişi Gönder");

    const submitButton = [...document.querySelectorAll("button")].find((b) => b.textContent === "Siparişi Gönder")!;
    await act(async () => submitButton.click());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Siparişiniz alındı");
    expect(document.body.textContent).toContain("Çorba");
  });

  it("when the table is not available for self-service, shows the blocking message from the server", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/catalog")) return jsonResponse(CATALOG);
      if (path.includes("/orders")) {
        return jsonResponse(
          { error: { code: "TABLE_NOT_AVAILABLE", message: "Bu masada şu anda kendi kendine sipariş verilemiyor, lütfen garsonu çağırın." } },
          409,
        );
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render("/nfc/table-1");
    await act(async () => Promise.resolve());
    const increase = document.querySelector<HTMLButtonElement>('[aria-label="Çorba adedini artır"]')!;
    await act(async () => increase.click());
    const submitButton = [...document.querySelectorAll("button")].find((b) => b.textContent === "Siparişi Gönder")!;
    await act(async () => submitButton.click());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("garsonu çağırın");
  });
});
