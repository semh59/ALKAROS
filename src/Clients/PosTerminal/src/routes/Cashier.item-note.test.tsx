// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });

vi.mock("@microsoft/signalr", () => ({
  LogLevel: { Warning: 2 },
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      return {
        on: () => undefined, onreconnecting: () => undefined, onreconnected: () => undefined, onclose: () => undefined,
        start: vi.fn(async () => undefined), stop: vi.fn(async () => undefined),
      };
    }
  },
}));

const sizeGroup = {
  modifierGroupId: "g1", code: "BOY", name: "Porsiyon boyu", selectionType: "Single", minSelections: 1, maxSelections: 1,
  modifiers: [{ modifierId: "m-big", code: "BIG", name: "Büyük boy", priceDelta: 5 }],
};
const catalog = [
  { productId: "p-pide", sku: "SKU-1", name: "Pide", categoryCode: "ANA", categoryName: "Ana Yemek", unitPrice: 100, taxRate: 10, modifierGroups: [sizeGroup] },
  { productId: "p-ayran", sku: "SKU-2", name: "Ayran", categoryCode: "ICE", categoryName: "İçecek", unitPrice: 20, taxRate: 10 },
];
const emptyOrder = {
  displayId: "d", terminalId: "terminal", orderId: "order-1", revision: 3, state: "Active", editable: true,
  orderNumber: "1", lines: [], subtotal: 0, discountTotal: 0, taxTotal: 0, total: 0, currency: "TRY", serverTimestamp: "", message: "",
};
const ayranLine = {
  itemId: "line-1", name: "Ayran", quantity: 1, unitPrice: 20, lineTotal: 20, discountAmount: 0, productId: "p-ayran", unsent: true,
};

let root: Root | null = null;

afterEach(() => {
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function openTill(order: Record<string, unknown> = emptyOrder, options: { failAdd?: boolean; capabilities?: string[]; hold?: Promise<void> } = {}) {
  const added: Record<string, unknown>[] = [];
  let lines = order.lines as unknown[];
  let revision = 3;
  vi.stubGlobal("matchMedia", () => ({ matches: false, media: "", addEventListener: () => undefined, removeEventListener: () => undefined, addListener: () => undefined, removeListener: () => undefined, onchange: null, dispatchEvent: () => false }));
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    const method = init?.method ?? "GET";
    if (path.includes("/auth/session")) return json({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal", capabilities: options.capabilities ?? ["orders.create"] });
    if (path.endsWith("/catalog")) return json(catalog);
    if (path.endsWith("/orders/active")) return json({ ...order, lines, revision });
    if (path.endsWith("/health/ready")) return json({ status: "ready" });
    if (path.includes("/orders/order-1/items")) {
      if (method === "POST") {
        if (options.failAdd) return json({ error: { code: "FAILED", message: "Ekleme başarısız.", status: 500 } }, 500);
        const body = JSON.parse(String(init!.body)) as Record<string, unknown>;
        if (options.hold && added.length === 0) await options.hold;
        added.push(body);
        lines = [...lines, { ...ayranLine, itemId: `new-${added.length}`, specialInstructions: body.specialInstructions }];
      } else if (method === "DELETE") {
        lines = [];
      }
      revision += 1;
      return json({ orderId: "order-1", revision });
    }
    return json({});
  }));
  const { Cashier } = await import("./Cashier");
  const { RouterProvider } = await import("../router");
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  await act(async () => { root!.render(createElement(RouterProvider, null, createElement(Cashier))); });
  for (let i = 0; i < 3; i++) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
  return added;
}

const settle = async () => { for (let i = 0; i < 3; i++) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); }); };
const tap = async (element: Element) => {
  await act(async () => { element.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
  await settle();
};
const button = (label: string) => [...document.querySelectorAll("button")].find((candidate) => candidate.textContent === label);
const card = (name: string) => [...document.querySelectorAll<HTMLButtonElement>(".product-card")].find((candidate) => candidate.textContent?.includes(name))!;
const noteInput = () => document.querySelector<HTMLInputElement>('input[aria-label="Sıradaki ürüne not"]');
const typeNote = async (text: string) => {
  await tap(button("Not ekle")!);
  const input = noteInput()!;
  await act(async () => {
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(input, text);
    input.dispatchEvent(new Event("input", { bubbles: true }));
  });
};

describe("the till's optional line note", () => {
  it("leaves the flow without a note exactly as it was: one tap on a product is one request without a note field", async () => {
    const added = await openTill();

    expect(noteInput()).toBeNull();
    await tap(card("Ayran"));

    expect(added).toHaveLength(1);
    expect(added[0]).not.toHaveProperty("specialInstructions");
    expect(noteInput()).toBeNull();
  });

  it("opens one short field with one tap, sends the note with the next product only and closes after the add", async () => {
    const added = await openTill();

    await typeNote("  soğansız ");
    expect(noteInput()!.maxLength).toBe(200);
    expect(document.body.textContent).toContain("Kişisel bilgi yazmayın.");
    await tap(card("Ayran"));
    await tap(card("Ayran"));

    expect(added).toHaveLength(2);
    expect(added[0]).toMatchObject({ productId: "p-ayran", specialInstructions: "soğansız" });
    expect(added[1]).not.toHaveProperty("specialInstructions");
    expect(noteInput()).toBeNull();
    expect(button("Not ekle")).toBeDefined();
  });

  it("treats a note of spaces as no note", async () => {
    const added = await openTill();

    await typeNote("   ");
    await tap(card("Ayran"));

    expect(added[0]).not.toHaveProperty("specialInstructions");
  });

  it("keeps the note when the add fails, so the same note can be sent again", async () => {
    await openTill(emptyOrder, { failAdd: true });

    await typeNote("az pişmiş");
    await tap(card("Ayran"));

    expect(noteInput()!.value).toBe("az pişmiş");
  });

  it("does not queue a tap behind a running add while a note waits: the tap is refused plainly and the note stays for the next product", async () => {
    let release = () => undefined as void;
    const hold = new Promise<void>((resolve) => { release = resolve; });
    const added = await openTill(emptyOrder, { hold });

    await tap(card("Ayran"));
    await typeNote("az pişmiş");
    await tap(card("Ayran"));
    expect(document.body.textContent).toContain("Ayran eklenmedi");
    expect(noteInput()!.value).toBe("az pişmiş");
    await act(async () => { release(); await new Promise((resolve) => setTimeout(resolve, 0)); });
    await settle();

    expect(added).toHaveLength(1);
    expect(added[0]).not.toHaveProperty("specialInstructions");
    expect(noteInput()!.value).toBe("az pişmiş");
  });

  it("sends the note together with the chosen options of a product that has option groups", async () => {
    const added = await openTill();

    await typeNote("kenarı ince");
    await tap(card("Pide"));
    await tap(document.querySelector<HTMLInputElement>('input[type="radio"]')!);
    await tap(button("Siparişe ekle")!);

    expect(added).toHaveLength(1);
    expect(added[0]).toMatchObject({ productId: "p-pide", modifiers: [{ modifierId: "m-big" }], specialInstructions: "kenarı ince" });
  });

  it("shows a line's note in the cart and puts it back with the line", async () => {
    const added = await openTill({ ...emptyOrder, lines: [{ ...ayranLine, specialInstructions: "soğansız" }] });

    expect(document.querySelector(".ticket-line")!.textContent).toContain("Not: soğansız");
    await tap(button("Kaldır")!);
    await tap(button("Evet, kaldır")!);
    await tap(button("Geri al")!);

    expect(added).toHaveLength(1);
    expect(added[0]).toMatchObject({ productId: "p-ayran", specialInstructions: "soğansız" });
  });

  it("does not offer the note while products go to the kitchen-less path, which carries none", async () => {
    await openTill(
      { ...emptyOrder, lines: [{ ...ayranLine, unsent: false }] },
      { capabilities: ["orders.create", "orders.add-without-kitchen"] },
    );
    expect(button("Not ekle")).toBeDefined();

    await tap(button("Mutfağa göndermeden hesaba ekle")!);
    await tap(document.querySelector<HTMLButtonElement>(".kitchenless__reasons button.secondary")!);

    expect(button("Not ekle")).toBeUndefined();
    expect(noteInput()).toBeNull();
  });

  it("drops a waiting note when the cashier enters the kitchen-less mode, so it cannot reappear on a later product", async () => {
    const added = await openTill(
      { ...emptyOrder, lines: [{ ...ayranLine, unsent: false }] },
      { capabilities: ["orders.create", "orders.add-without-kitchen"] },
    );

    await typeNote("soğansız");
    await tap(button("Mutfağa göndermeden hesaba ekle")!);
    await tap(document.querySelector<HTMLButtonElement>(".kitchenless__reasons button.secondary")!);
    await tap(button("Normal eklemeye dön")!);

    expect(noteInput()).toBeNull();
    await tap(card("Ayran"));
    expect(added.at(-1)).not.toHaveProperty("specialInstructions");
  });
});
