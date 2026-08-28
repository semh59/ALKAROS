// @vitest-environment jsdom

import { act, type ComponentProps, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { CatalogWorkspace, type CatalogData } from "./index";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const data: CatalogData = {
  categories: [{ id: "cat-1", code: "HOT", name: "Sıcak içecek", parentId: null, sortOrder: 1, active: true }],
  taxes: [{ id: "tax-1", code: "VAT10", name: "KDV %10", vatRate: 10, active: true }],
  products: [{ id: "product-1", sku: "ESP-01", name: "Espresso", productType: "MenuItem", stockMode: "Untracked", categoryId: "cat-1", taxProfileId: "tax-1", description: null, printerRoutePolicy: null, displayOrder: 1, currentPrice: 95, active: true }],
  modifierGroups: [{ id: "group-1", code: "MILK", name: "Süt seçimi", selectionType: "SelectOne", minSelections: 0, maxSelections: 1, active: true }],
  modifiers: [{ id: "modifier-1", modifierGroupId: "group-1", code: "OAT", name: "Yulaf sütü", priceDelta: 15, productId: null, active: true }],
  prices: [{ id: "price-1", productId: "product-1", priceType: "SalePrice", price: 95, currencyCode: "TRY", effectiveFrom: "2026-01-01T00:00:00Z", effectiveTo: null }],
};

function baseProps(overrides: Partial<ComponentProps<typeof CatalogWorkspace>> = {}) {
  return { state: "ready" as const, data, canManage: true, onRefresh: vi.fn(), onCreate: vi.fn(), ...overrides };
}

describe("catalog workspace", () => {
  let root: Root | null = null;
  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.title = "ALKAROS catalog";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }
  async function click(element: Element) { await act(async () => (element as HTMLElement).click()); }
  async function fill(input: HTMLInputElement, value: string) {
    await act(async () => {
      const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;
      setter.call(input, value);
      input.dispatchEvent(new Event("input", { bubbles: true }));
      input.dispatchEvent(new Event("change", { bubbles: true }));
    });
  }
  afterEach(async () => { if (root) await act(async () => root!.unmount()); root = null; vi.restoreAllMocks(); });

  it("renders searchable entity tabs with authoritative product detail", async () => {
    await render(<CatalogWorkspace {...baseProps()} />);
    expect(document.body.textContent).toContain("Espresso");
    expect(document.body.textContent).toContain("95.00 TRY");
    expect(document.body.textContent).toContain("Stok modu");
    const search = document.querySelector<HTMLInputElement>(".catalog-workspace__toolbar input")!;
    await fill(search, "yok");
    expect(document.body.textContent).toContain("Eşleşen kayıt yok");
    await fill(search, "ESP");
    expect(document.querySelectorAll(".catalog-row")).toHaveLength(1);
  });

  it("validates manager product price and keeps the form on a conflict", async () => {
    const onCreate = vi.fn().mockRejectedValue(new Error("409 duplicate SKU"));
    await render(<CatalogWorkspace {...baseProps({ onCreate })} />);
    await click([...document.querySelectorAll("button")].find((button) => button.textContent?.includes("Ürün ekle"))!);
    const dialog = document.querySelector('[role="dialog"]')!;
    await click([...dialog.querySelectorAll("button")].find((button) => button.textContent === "Kaydı oluştur")!);
    expect(dialog.textContent).toContain("SKU alanı gerekli.");
    const inputs = dialog.querySelectorAll<HTMLInputElement>("input");
    await fill(inputs[0], "ESP-01");
    await fill(inputs[1], "Espresso");
    await fill(inputs[2], "95");
    await click([...dialog.querySelectorAll("button")].find((button) => button.textContent === "Kaydı oluştur")!);
    expect(document.body.textContent).toContain("Kayıt çakıştı");
    expect(dialog.querySelector<HTMLInputElement>("input")?.value).toBe("ESP-01");
  });

  it.each([
    ["loading", "Catalog yükleniyor"], ["busy", "Catalog kaydediliyor"], ["offline", "Bağlantı yok"],
    ["unauthorized", "Manager oturumu gerekli"], ["error", "Catalog alınamadı"], ["stale", "Catalog güncel değil"], ["conflict", "Catalog çakışması"],
  ] as const)("shows bounded %s state", async (state, title) => {
    await render(<CatalogWorkspace {...baseProps({ state, errorMessage: "API kapalı" })} />);
    expect(document.body.textContent).toContain(title);
  });

  it("has no critical or serious axe violations", async () => {
    await render(<CatalogWorkspace {...baseProps()} />);
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
