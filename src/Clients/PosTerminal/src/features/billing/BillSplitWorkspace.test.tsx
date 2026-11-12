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

  it("names the table of the bill and marks the lines the cashier added without the kitchen", async () => {
    const withNote: BillSplitDesign = { ...design, tableNumber: "T-4", items: [design.items[0], { ...design.items[1], addedWithoutKitchenReason: "waiter-forgot" }] };
    await render(<BillSplitWorkspace {...props({ design: withNote })} />);
    expect(document.querySelector(".bill-split__table")?.textContent).toBe("Masa T-4");
    await click([...document.querySelectorAll('[role="tab"]')].find((tab) => tab.textContent === "Ürün / miktar")!);
    const notes = [...document.querySelectorAll(".bill-split__added")];
    expect(notes.map((note) => note.textContent)).toEqual(["Kasada eklendi (mutfağa gitmedi)"]);
    expect(notes[0].closest("article")?.textContent).toContain("Ayran");

    await render(<BillSplitWorkspace {...props()} />);
    expect(document.querySelector(".bill-split__table")).toBeNull();
    expect(document.querySelector(".bill-split__added")).toBeNull();
  });

  it("keeps only the selected mode tab in the tab order, and moves that stop with the selection", async () => {
    await render(<BillSplitWorkspace {...props()} />);
    const tabStops = () => [...document.querySelectorAll<HTMLElement>('[role="tab"]')].filter((tab) => tab.tabIndex === 0);
    const first = document.querySelector('[role="tab"][aria-selected="true"]')!;
    expect(document.querySelectorAll('[role="tab"]').length).toBeGreaterThan(1);
    expect(tabStops()).toEqual([first]);

    await act(async () => { first.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowRight", bubbles: true, cancelable: true })); });

    const moved = document.querySelector('[role="tab"][aria-selected="true"]')!;
    expect(moved).not.toBe(first);
    expect(tabStops()).toEqual([moved]);
  });

  it("switches the split mode with the right arrow on the mode tabs", async () => {
    await render(<BillSplitWorkspace {...props()} />);
    const first = document.querySelector('[role="tab"][aria-selected="true"]')!;
    await act(async () => { first.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowRight", bubbles: true, cancelable: true })); });
    const selected = document.querySelector('[role="tab"][aria-selected="true"]')!;
    expect(selected).not.toBe(first);
    expect(document.activeElement).toBe(selected);
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

  it("shows a fixed Turkish sentence for the error code of a real BillingSplitApiError, never the server text", async () => {
    const onSave = vi.fn().mockRejectedValue(new BillingSplitApiError(409, "ITEM_SPLIT_ON_ADJUSTED_BILL", "raw english server text"));
    await render(<BillSplitWorkspace {...props({ onSave })} />);
    const checks = [...document.querySelectorAll<HTMLInputElement>('.bill-split__owner-picker input')];
    await click(checks[0]); await click(checks[1]);
    await click([...document.querySelectorAll("button")].find((button) => button.textContent === "Dağıtımı kaydet")!);
    expect(document.querySelector('[role="alert"]')?.textContent).toContain("ürün bazında bölünemez");
    expect(document.body.textContent).not.toContain("raw english server text");
  });

  it("tells a conflict by its status, not by words in the server message", async () => {
    const onSave = vi.fn().mockRejectedValue(new BillingSplitApiError(422, "VERSION_TEXT", "Sürüm bilgisi geçersiz (version)."));
    await render(<BillSplitWorkspace {...props({ onSave })} />);
    const checks = [...document.querySelectorAll<HTMLInputElement>('.bill-split__owner-picker input')];
    await click(checks[0]); await click(checks[1]);
    await click([...document.querySelectorAll("button")].find((button) => button.textContent === "Dağıtımı kaydet")!);
    expect(document.querySelector('[role="alert"]')?.textContent).toContain("Hesap dağıtımı kaydedilemedi.");
    expect(document.body.textContent).not.toContain("Sürüm bilgisi geçersiz");
    expect(document.querySelector('[role="alert"]')?.textContent).not.toContain("Taslağınız korundu");
  });

  it("tells the cashier there is no active bill as information, not as a failed load", async () => {
    await render(<BillSplitWorkspace {...props({ state: "empty", design: null })} />);
    const text = document.body.textContent ?? "";
    expect(text).toContain("Aktif adisyon yok");
    expect(text).toContain("Bir masa veya siparişten hesap açın.");
    expect(text).not.toContain("Hesap alınamadı");
  });

  it("shows no English text, version number or raw owner kind", async () => {
    await render(<BillSplitWorkspace {...props({ state: "loading", design: null })} />);
    expect(document.body.textContent).toContain("Güncel hesap ve dağıtımlar alınıyor");
    expect(document.body.textContent).not.toContain("Authoritative");

    await act(async () => root!.unmount());
    await render(<BillSplitWorkspace {...props()} />);
    expect(document.body.textContent).toContain("Henüz güncellenmedi");
    expect(document.body.textContent).not.toMatch(/v7|Person|Seat/);
    await click([...document.querySelectorAll('[role="tab"]')].find((tab) => tab.textContent === "Tutar gir")!);
    const guest = [...document.querySelectorAll(".bill-split__amounts label")].find((label) => label.textContent?.includes("1. Kişi"))!;
    expect(guest.querySelector("small")?.textContent).toBe("Kişi");
    expect(document.body.textContent).not.toMatch(/Person|Seat/);
  });

  it("shows when the bill was last refreshed as the venue (Istanbul) time", async () => {
    await render(<BillSplitWorkspace {...props({ lastUpdated: "2026-10-13T07:05:00Z" })} />);
    expect(document.body.textContent).toContain("Güncellendi 10:05");
  });

  it("has no critical or serious automated accessibility findings", async () => {
    await render(<BillSplitWorkspace {...props()} />);
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect([...report.violations, ...report.incomplete].filter((item) => item.impact === "critical" || item.impact === "serious")).toEqual([]);
  });
});
