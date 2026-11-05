// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { CatalogWorkspace, type CatalogData } from "./index";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const product = (allowsHalfPortion: boolean) => ({
  id: "product-1", sku: "ESP-01", name: "Espresso", productType: "MenuItem", stockMode: "Untracked", categoryId: "cat-1", taxProfileId: "tax-1",
  description: null, printerRoutePolicy: null, displayOrder: 1, currentPrice: 95, active: true, isAvailable: true, prepTimeMinutes: null, allowsHalfPortion,
});
const data = (allowsHalfPortion: boolean): CatalogData => ({
  categories: [{ id: "cat-1", code: "HOT", name: "Sıcak içecek", parentId: null, sortOrder: 1, active: true }],
  taxes: [{ id: "tax-1", code: "VAT10", name: "KDV %10", vatRate: 10, active: true }],
  modifierGroups: [], modifiers: [], prices: [], assignments: [], products: [product(allowsHalfPortion)],
});

describe("half portion checkbox of the product form", () => {
  let root: Root | null = null;
  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }
  const click = async (element: Element) => act(async () => (element as HTMLElement).click());
  const button = (scope: ParentNode, label: string) => [...scope.querySelectorAll("button")].find((b) => b.textContent?.includes(label))!;
  async function fill(input: HTMLInputElement, value: string) {
    await act(async () => {
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(input, value);
      input.dispatchEvent(new Event("input", { bubbles: true }));
    });
  }
  afterEach(async () => { if (root) await act(async () => root!.unmount()); root = null; });

  it("creates a product without the flag by default and with it when the box is checked", async () => {
    for (const checked of [false, true]) {
      const onCreate = vi.fn().mockResolvedValue(undefined);
      await render(<CatalogWorkspace state="ready" data={{ ...data(false), products: [] }} canManage onRefresh={vi.fn()} onCreate={onCreate} />);
      await click(button(document, "Ürün ekle"));
      const dialog = document.querySelector('[role="dialog"]')!;
      const box = dialog.querySelector<HTMLInputElement>('input[type="checkbox"]')!;
      expect(box.checked).toBe(false);
      expect(dialog.textContent).toContain("Yarım porsiyon olur");
      const inputs = dialog.querySelectorAll<HTMLInputElement>("input");
      await fill(inputs[0], "ESP-01");
      await fill(inputs[1], "Espresso");
      await fill(inputs[2], "95");
      const tax = [...dialog.querySelectorAll("label")].find((l) => l.textContent?.startsWith("KDV profili"))!.querySelector("select")!;
      await act(async () => {
        Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, "value")!.set!.call(tax, "tax-1");
        tax.dispatchEvent(new Event("change", { bubbles: true }));
      });
      if (checked) await click(box);
      await click(button(dialog, "Kaydı oluştur"));

      expect(onCreate.mock.calls[0][0].value.allowsHalfPortion).toBe(checked);
      await act(async () => root!.unmount());
    }
  });

  it("shows the stored flag on edit and sends the changed value with the details", async () => {
    const onUpdateProduct = vi.fn().mockResolvedValue(undefined);
    await render(<CatalogWorkspace state="ready" data={data(true)} canManage onRefresh={vi.fn()} onUpdateProduct={onUpdateProduct} />);
    await click(button(document.querySelector(".catalog-details")!, "Ürünü düzenle"));
    const dialog = document.querySelector('[role="dialog"]')!;
    const box = dialog.querySelector<HTMLInputElement>('input[type="checkbox"]')!;
    expect(box.checked).toBe(true);
    await click(box);
    await click(button(dialog, "Kaydet"));

    expect(onUpdateProduct).toHaveBeenCalledWith("product-1", expect.objectContaining({ allowsHalfPortion: false }));
  });
});
