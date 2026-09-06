// @vitest-environment jsdom

import { act, type ComponentProps, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../../api";
import { CatalogWorkspace, type CatalogData } from "./index";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const data: CatalogData = {
  categories: [{ id: "cat-1", code: "HOT", name: "Sıcak içecek", parentId: null, sortOrder: 1, active: true }],
  taxes: [{ id: "tax-1", code: "VAT10", name: "KDV %10", vatRate: 10, active: true }],
  products: [{ id: "product-1", sku: "ESP-01", name: "Espresso", productType: "MenuItem", stockMode: "Untracked", categoryId: "cat-1", taxProfileId: "tax-1", description: null, printerRoutePolicy: null, displayOrder: 1, currentPrice: 95, active: true, isAvailable: true }],
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
    // V1-RMD-114: a real conflict always reaches this component as an
    // ApiError; a plain Error is now correctly treated as an untrusted
    // client-side/network failure instead.
    const onCreate = vi.fn().mockRejectedValue(new ApiError(409, "CONCURRENCY_CONFLICT", "409 duplicate SKU"));
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
    ["loading", "Katalog yükleniyor"], ["busy", "Katalog kaydediliyor"], ["offline", "Bağlantı yok"],
    ["unauthorized", "Yönetici oturumu gerekli"], ["error", "Katalog alınamadı"], ["stale", "Katalog güncel değil"], ["conflict", "Katalog çakışması"],
  ] as const)("shows bounded %s state", async (state, title) => {
    await render(<CatalogWorkspace {...baseProps({ state, errorMessage: "API kapalı" })} />);
    expect(document.body.textContent).toContain(title);
  });

  it("suspends a product from the menu with a single action", async () => {
    const onSetAvailability = vi.fn().mockResolvedValue(undefined);
    await render(<CatalogWorkspace {...baseProps({ onSetAvailability })} />);
    const toggle = [...document.querySelectorAll("button")].find((button) => button.textContent?.includes("Menüden kaldır"))!;
    expect(toggle).toBeTruthy();
    await click(toggle);
    expect(onSetAvailability).toHaveBeenCalledWith("product-1", false);
  });

  it("shows the restore action for an already suspended product", async () => {
    const suspended = { ...data, products: [{ ...data.products[0], isAvailable: false }] };
    await render(<CatalogWorkspace {...baseProps({ data: suspended, onSetAvailability: vi.fn() })} />);
    expect(document.body.textContent).toContain("Menüden kaldırıldı");
    expect([...document.querySelectorAll("button")].some((button) => button.textContent?.includes("Menüye geri al"))).toBe(true);
  });

  it("uses the Turkish term Katalog and never the English Catalog in visible text or labels", async () => {
    await render(<CatalogWorkspace {...baseProps()} />);
    expect(document.body.textContent).not.toContain("Catalog");
    const searchInput = document.querySelector('input[aria-label]');
    expect(searchInput!.getAttribute("aria-label")).toBe("Katalog ara");
    const nav = document.querySelector(".catalog-workspace__tabs");
    expect(nav!.getAttribute("aria-label")).toBe("Katalog kaynak türü");
  });

  it("has no critical or serious axe violations", async () => {
    await render(<CatalogWorkspace {...baseProps()} />);
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
