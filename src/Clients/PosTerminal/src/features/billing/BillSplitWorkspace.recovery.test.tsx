// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { BillingSplitApiError } from "./billingApi";
import { BillSplitWorkspace } from "./BillSplitWorkspace";
import type { BillSplitDesign, BillSplitWorkspaceProps, SplitOwnerOption } from "./models";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const owners: SplitOwnerOption[] = [
  { kind: "Seat", ownerId: "seat-1", label: "Sandalye 1" },
  { kind: "Seat", ownerId: "seat-2", label: "Sandalye 2" },
];
const design: BillSplitDesign = {
  billId: "bill-1", billNumber: "H-1042", billStatus: "Open", currencyCode: "TRY",
  payableAmount: 275, taxTotal: 25, billRowVersion: 7, mode: "None", executionState: "DesignOnly",
  allowedCommands: ["SaveEqual", "SaveItems", "SaveAmounts", "Clear"],
  items: [
    { billItemId: "item-1", productName: "Pizza", quantity: 2, grossAmount: 220, taxAmount: 20, rowVersion: 2 },
    { billItemId: "item-2", productName: "Ayran", quantity: 1, grossAmount: 55, taxAmount: 5, rowVersion: 1 },
  ],
  allocations: [],
};
const saved: BillSplitDesign = {
  ...design, mode: "ByAmount",
  allocations: [{ allocationId: "a1", mode: "ByAmount", ownerKind: "Seat", ownerId: "seat-1", legacyOwnerReference: null, billItemId: null, quantity: null, amount: 275, taxAmount: 25, rowVersion: 1 }],
};

describe("bill split workspace recovery and empty states", () => {
  let root: Root | null = null;
  const props = (overrides: Partial<BillSplitWorkspaceProps> = {}): BillSplitWorkspaceProps => ({
    state: "ready", design, owners, canMutate: true, onRefresh: vi.fn(), onSave: vi.fn(), onClear: vi.fn(), ...overrides,
  });
  async function mount(element: ReactElement) {
    document.body.innerHTML = '<main id="root"></main>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }
  async function rerender(element: ReactElement) { await act(async () => root!.render(element)); }
  const button = (text: string) => [...document.querySelectorAll("button")].find((b) => b.textContent === text)!;
  const click = (element: Element) => act(async () => (element as HTMLElement).click());
  async function input(element: HTMLInputElement, value: string) {
    await act(async () => {
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(element, value);
      element.dispatchEvent(new Event("input", { bubbles: true }));
      element.dispatchEvent(new Event("change", { bubbles: true }));
    });
  }
  const tab = (name: string) => click([...document.querySelectorAll('[role="tab"]')].find((t) => t.textContent === name)!);
  afterEach(async () => { if (root) await act(async () => root!.unmount()); root = null; });

  it("recovers from a 409: refresh keeps the draft and the new bill version is used", async () => {
    const conflict = new BillingSplitApiError(409, "CONCURRENCY_CONFLICT", "x");
    const onRefresh = vi.fn();
    await mount(<BillSplitWorkspace {...props({ onSave: vi.fn().mockRejectedValue(conflict), onRefresh })} />);
    const checks = [...document.querySelectorAll<HTMLInputElement>(".bill-split__owner-picker input")];
    await click(checks[0]); await click(checks[1]);
    await click(button("Dağıtımı kaydet"));
    await click(button("Hesabı yenile"));
    expect(onRefresh).toHaveBeenCalledOnce();

    await rerender(<BillSplitWorkspace {...props({ design: { ...design, billNumber: "H-1042-v2", billRowVersion: 8 } })} />);

    expect(document.body.textContent).toContain("H-1042-v2");
    expect([...document.querySelectorAll<HTMLInputElement>(".bill-split__owner-picker input")].every((c) => c.checked)).toBe(true);
  });

  it("lets the cashier throw the stale draft away", async () => {
    await mount(<BillSplitWorkspace {...props({ onSave: vi.fn().mockRejectedValue(new BillingSplitApiError(409, "CONCURRENCY_CONFLICT", "x")) })} />);
    const checks = [...document.querySelectorAll<HTMLInputElement>(".bill-split__owner-picker input")];
    await click(checks[0]); await click(checks[1]);
    await click(button("Dağıtımı kaydet"));
    await click(button("Taslağı at"));

    expect([...document.querySelectorAll<HTMLInputElement>(".bill-split__owner-picker input")].some((c) => c.checked)).toBe(false);
  });

  it("does not call an item-split refusal on an adjusted bill a concurrent change", async () => {
    const refusal = new BillingSplitApiError(409, "ITEM_SPLIT_ON_ADJUSTED_BILL", "İndirim uygulanmış hesap ürün bazında bölünemez.");
    await mount(<BillSplitWorkspace {...props({ onSave: vi.fn().mockRejectedValue(refusal) })} />);
    await tab("Ürün / miktar");
    const cells = [...document.querySelectorAll<HTMLInputElement>(".bill-split__items input")];
    await input(cells[0], "1");
    await click(button("Dağıtımı kaydet"));

    const alert = document.querySelector('[role="alert"]')!.textContent!;
    expect(alert).toContain("ürün bazında bölünemez");
    expect(alert).not.toContain("başka bir yerde değişti");
    expect(document.body.textContent).not.toContain("Hesabı yenile");
  });

  it("tells the cashier up front that a discounted bill cannot be split by item", async () => {
    await mount(<BillSplitWorkspace {...props({ design: { ...design, payableAmount: 247.5 } })} />);
    await tab("Ürün / miktar");

    expect(document.body.textContent).toContain("ürün bazında bölme kullanılamaz");
  });

  it("an emptied amount cell is not a zero amount", async () => {
    await mount(<BillSplitWorkspace {...props()} />);
    await tab("Tutar gir");
    const fields = [...document.querySelectorAll<HTMLInputElement>(".bill-split__amounts input")];
    await input(fields[0], "100"); await input(fields[1], "175");
    await input(fields[1], "");

    expect(document.body.textContent).not.toContain("sıfırdan büyük olmalı");
  });

  it("says so when the bill has no seats or people instead of showing an empty editor", async () => {
    await mount(<BillSplitWorkspace {...props({ owners: [] })} />);

    expect(document.querySelector(".bill-split__empty")?.textContent).toContain("kayıtlı sandalye veya kişi yok");
    expect(button("Dağıtımı kaydet").disabled).toBe(true);
  });

  it("asks before wiping a saved split", async () => {
    const onClear = vi.fn().mockResolvedValue(design);
    await mount(<BillSplitWorkspace {...props({ design: saved, onClear })} />);

    await click(button("Dağıtımı sıfırla"));
    expect(onClear).not.toHaveBeenCalled();
    await click(button("Vazgeç"));
    expect(onClear).not.toHaveBeenCalled();
    await click(button("Dağıtımı sıfırla"));
    await click(button("Evet, sıfırla"));
    expect(onClear).toHaveBeenCalledOnce();
  });
});
