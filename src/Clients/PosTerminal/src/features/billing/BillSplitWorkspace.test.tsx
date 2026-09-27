// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../../api";
import { BillingSplitApiError } from "./billingApi";
import { BillSplitWorkspace } from "./BillSplitWorkspace";
import type { BillSplitDesign, BillSplitWorkspaceProps, SplitOwnerOption } from "./models";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const owners: SplitOwnerOption[] = [
  { kind: "Seat", ownerId: "seat-1", label: "Sandalye 1", secondaryLabel: "A-01" },
  { kind: "Seat", ownerId: "seat-2", label: "Sandalye 2", secondaryLabel: "A-01" },
  { kind: "Person", ownerId: "person-1", label: "Misafir 1" },
];
const design: BillSplitDesign = {
  billId: "bill-1", billNumber: "H-1042", billStatus: "Open", currencyCode: "TRY",
  payableAmount: 275, taxTotal: 25, billRowVersion: 7, mode: "None", executionState: "DesignOnly",
  allowedCommands: ["SaveEqual", "SaveItems", "SaveAmounts", "Clear"],
  items: [
    { billItemId: "item-1", productName: "Taş fırın pizza", quantity: 2, grossAmount: 220, taxAmount: 20, rowVersion: 2 },
    { billItemId: "item-2", productName: "Ayran", quantity: 1, grossAmount: 55, taxAmount: 5, rowVersion: 1 },
  ],
  allocations: [],
};

describe("bill split workspace", () => {
  let root: Root | null = null;
  const props = (overrides: Partial<BillSplitWorkspaceProps> = {}): BillSplitWorkspaceProps => ({ state: "ready", design, owners, canMutate: true, onRefresh: vi.fn(), onSave: vi.fn().mockResolvedValue({ ...design, mode: "ByAmount", billRowVersion: 8 }), onClear: vi.fn(), ...overrides });
  async function render(element: ReactElement) { document.documentElement.lang = "tr"; document.title = "ALKAROS hesap dağıtımı"; document.body.innerHTML = '<main id="root"></main>'; root = createRoot(document.getElementById("root")!); await act(async () => root!.render(element)); }
  async function click(element: Element) { await act(async () => (element as HTMLElement).click()); }
  async function input(element: HTMLInputElement, value: string) { await act(async () => { const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!; setter.call(element, value); element.dispatchEvent(new Event("input", { bubbles: true })); element.dispatchEvent(new Event("change", { bubbles: true })); }); }
  afterEach(async () => { if (root) await act(async () => root!.unmount()); root = null; vi.restoreAllMocks(); });

  it("shows authoritative totals, items, owners and design-only language", async () => {
    await render(<BillSplitWorkspace {...props()} />);
    expect(document.body.textContent).toContain("H-1042");
    expect(document.body.textContent).toContain("₺275,00");
    expect(document.body.textContent).toContain("Sandalye 1");
    expect(document.body.textContent).toContain("tahsilat veya ödeme yürütmez");
  });

  it("supports a complete amount draft and submits authoritative design context", async () => {
    const onSave = vi.fn().mockResolvedValue({ ...design, mode: "ByAmount", billRowVersion: 8 });
    await render(<BillSplitWorkspace {...props({ onSave })} />);
    await click([...document.querySelectorAll('[role="tab"]')].find((tab) => tab.textContent === "Tutar gir")!);
    const fields = [...document.querySelectorAll<HTMLInputElement>('.bill-split__amounts input')];
    await input(fields[0], "100"); await input(fields[1], "175");
    await click([...document.querySelectorAll("button")].find((button) => button.textContent === "Dağıtımı kaydet")!);
    expect(onSave).toHaveBeenCalledWith({ mode: "ByAmount", targets: [{ owner: { kind: "Seat", ownerId: "seat-1" }, amount: 100 }, { owner: { kind: "Seat", ownerId: "seat-2" }, amount: 175 }] }, design);
    expect(document.body.textContent).toContain("Ödeme işlemi yapılmadı");
  });

  it("blocks cumulative item quantity overflow", async () => {
    await render(<BillSplitWorkspace {...props()} />);
    await click([...document.querySelectorAll('[role="tab"]')].find((tab) => tab.textContent === "Ürün / miktar")!);
    const pizza = [...document.querySelectorAll<HTMLInputElement>('.bill-split__items input')].slice(0, 3);
    await input(pizza[0], "1.5"); await input(pizza[1], "1");
    expect(document.body.textContent).toContain("toplam miktarı 2 değerini aşıyor");
    expect([...document.querySelectorAll<HTMLButtonElement>("button")].find((button) => button.textContent === "Dağıtımı kaydet")?.disabled).toBe(true);
  });

  it("preserves a dirty draft after a concurrency conflict", async () => {
    // V1-RMD-114: a real conflict always reaches this component as an
    // ApiError; a plain Error is now correctly treated as an untrusted
    // client-side/network failure instead.
    await render(<BillSplitWorkspace {...props({ onSave: vi.fn().mockRejectedValue(new ApiError(409, "CONCURRENCY_CONFLICT", "409 concurrent version")) })} />);
    const checks = [...document.querySelectorAll<HTMLInputElement>('.bill-split__owner-picker input')];
    await click(checks[0]); await click(checks[1]);
    await click([...document.querySelectorAll("button")].find((button) => button.textContent === "Dağıtımı kaydet")!);
    expect(document.querySelector('[role="alert"]')?.textContent).toContain("Taslağınız korundu");
    expect(checks[0].checked).toBe(true); expect(checks[1].checked).toBe(true);
  });

  it("shows the backend's own Turkish message for a real BillingSplitApiError, not the generic fallback", async () => {
    // V1-RMD-366 (module-by-module UI audit, 2026-09-27): onSave/onClear's
    // REAL failures reach this component as billingApi.ts's own
    // BillingSplitApiError, not the shared ApiError the V1-RMD-114 test
    // above checks - that test's own mock (`new ApiError(...)`) never
    // actually exercised this file's `instanceof` check against the class
    // production code really throws, which is why this went unnoticed.
    const onSave = vi.fn().mockRejectedValue(new BillingSplitApiError(422, "ITEM_OVER_ALLOCATED", "Bir kalemin dağıtılan miktarı sunucuda artık geçerli değil."));
    await render(<BillSplitWorkspace {...props({ onSave })} />);
    const checks = [...document.querySelectorAll<HTMLInputElement>('.bill-split__owner-picker input')];
    await click(checks[0]); await click(checks[1]);
    await click([...document.querySelectorAll("button")].find((button) => button.textContent === "Dağıtımı kaydet")!);
    expect(document.querySelector('[role="alert"]')?.textContent).toContain("Bir kalemin dağıtılan miktarı sunucuda artık geçerli değil.");
  });

  it("has no critical or serious automated accessibility findings", async () => {
    await render(<BillSplitWorkspace {...props()} />);
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect([...report.violations, ...report.incomplete].filter((item) => item.impact === "critical" || item.impact === "serious")).toEqual([]);
  });
});
