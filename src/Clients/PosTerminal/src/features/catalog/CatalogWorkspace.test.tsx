// @vitest-environment jsdom

import { act, type ComponentProps, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../../api";
import { CatalogApiError, CatalogWorkspace, type CatalogData } from "./index";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const data: CatalogData = {
  categories: [{ id: "cat-1", code: "HOT", name: "Sıcak içecek", parentId: null, sortOrder: 1, active: true }],
  taxes: [{ id: "tax-1", code: "VAT10", name: "KDV %10", vatRate: 10, active: true }],
  products: [{ id: "product-1", sku: "ESP-01", name: "Espresso", productType: "MenuItem", stockMode: "Untracked", categoryId: "cat-1", taxProfileId: "tax-1", description: null, printerRoutePolicy: null, displayOrder: 1, currentPrice: 95, active: true, isAvailable: true, prepTimeMinutes: null }],
  modifierGroups: [{ id: "group-1", code: "MILK", name: "Süt seçimi", selectionType: "SelectOne", minSelections: 0, maxSelections: 1, active: true }],
  modifiers: [{ id: "modifier-1", modifierGroupId: "group-1", code: "OAT", name: "Yulaf sütü", priceDelta: 15, productId: null, active: true }],
  prices: [{ id: "price-1", productId: "product-1", priceType: "SalePrice", price: 95, currencyCode: "TRY", effectiveFrom: "2026-01-01T00:00:00Z", effectiveTo: null }],
  assignments: [],
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
  async function choose(dialog: Element, labelStart: string, value: string) {
    const select = [...dialog.querySelectorAll("label")].find((label) => label.textContent?.startsWith(labelStart))!.querySelector("select")!;
    await act(async () => {
      Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, "value")!.set!.call(select, value);
      select.dispatchEvent(new Event("change", { bubbles: true }));
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
    await choose(dialog, "KDV profili", "tax-1");
    await click([...dialog.querySelectorAll("button")].find((button) => button.textContent === "Kaydı oluştur")!);
    expect(document.body.textContent).toContain("Kayıt çakıştı");
    expect(dialog.querySelector<HTMLInputElement>("input")?.value).toBe("ESP-01");
  });

  it("shows a Turkish conflict inside the dialog by status code and plain errors without the conflict wording", async () => {
    const onCreate = vi.fn()
      .mockRejectedValueOnce(new ApiError(409, "OVERLAPPING_EFFECTIVE_PRICE", "Fiyat aralığı mevcut bir fiyatla çakışıyor."))
      .mockRejectedValueOnce(new ApiError(400, "VALIDATION_FAILED", "Bu SKU duplicate görünüyor."));
    await render(<CatalogWorkspace {...baseProps({ onCreate })} />);
    await click([...document.querySelectorAll("button")].find((button) => button.textContent?.includes("Ürün ekle"))!);
    const dialog = document.querySelector('[role="dialog"]')!;
    const inputs = dialog.querySelectorAll<HTMLInputElement>("input");
    await fill(inputs[0], "ESP-01");
    await fill(inputs[1], "Espresso");
    await fill(inputs[2], "95");
    await choose(dialog, "KDV profili", "tax-1");
    const submit = () => click([...dialog.querySelectorAll("button")].find((button) => button.textContent === "Kaydı oluştur")!);
    await submit();
    expect(dialog.textContent).toContain("Kayıt çakıştı: Fiyat aralığı mevcut bir fiyatla çakışıyor.");
    await submit();
    expect(dialog.textContent).toContain("Bu SKU duplicate görünüyor.");
    expect(dialog.textContent).not.toContain("Kayıt çakıştı");
  });

  it("sends a second submit while the first is pending only once", async () => {
    let finish: () => void = () => undefined;
    const onCreate = vi.fn().mockReturnValue(new Promise<void>((resolve) => { finish = resolve; }));
    await render(<CatalogWorkspace {...baseProps({ onCreate })} />);
    await click([...document.querySelectorAll("button")].find((button) => button.textContent?.includes("Ürün ekle"))!);
    const dialog = document.querySelector('[role="dialog"]')!;
    const inputs = dialog.querySelectorAll<HTMLInputElement>("input");
    await fill(inputs[0], "ESP-01");
    await fill(inputs[1], "Espresso");
    await fill(inputs[2], "95");
    await choose(dialog, "KDV profili", "tax-1");
    const form = dialog.querySelector("form")!;
    const submitForm = () => act(async () => { form.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true })); });
    await submitForm();
    await submitForm();
    expect(onCreate).toHaveBeenCalledTimes(1);
    await act(async () => finish());
  });

  it("retries a failed create with the same record id", async () => {
    const onCreate = vi.fn().mockRejectedValueOnce(new ApiError(500, "FAILED", "Sunucu hatası.")).mockResolvedValueOnce(undefined);
    await render(<CatalogWorkspace {...baseProps({ onCreate })} />);
    await click([...document.querySelectorAll("button")].find((button) => button.textContent?.includes("Ürün ekle"))!);
    const dialog = document.querySelector('[role="dialog"]')!;
    const inputs = dialog.querySelectorAll<HTMLInputElement>("input");
    await fill(inputs[0], "ESP-01");
    await fill(inputs[1], "Espresso");
    await fill(inputs[2], "95");
    await choose(dialog, "KDV profili", "tax-1");
    const submit = () => click([...dialog.querySelectorAll("button")].find((button) => button.textContent === "Kaydı oluştur")!);
    await submit();
    await submit();
    expect(onCreate).toHaveBeenCalledTimes(2);
    expect(onCreate.mock.calls[0][0].value.id).toBe(onCreate.mock.calls[1][0].value.id);
  });

  it("defaults the price start to the local clock, not UTC", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-01-01T10:00:00Z"));
    vi.spyOn(Date.prototype, "getTimezoneOffset").mockReturnValue(-180);
    try {
      await render(<CatalogWorkspace {...baseProps()} />);
      await click([...document.querySelectorAll("nav button")].find((button) => button.textContent?.startsWith("Fiyat"))!);
      await click([...document.querySelectorAll("button")].find((button) => button.textContent?.includes("ekle"))!);
      expect(document.querySelector<HTMLInputElement>('[role="dialog"] input[type="datetime-local"]')!.value).toBe("2026-01-01T13:00");
    } finally {
      vi.useRealTimers();
    }
  });

  it("does not offer the untracked stock mode the sale path ignores and shows older records in Turkish", async () => {
    // V1-RMD-437 (V1-RMD-398 G-11): an order is only accepted for a product with a stock mapping (V1-RMD-143),
    // whatever its stock mode, so "Untracked" is no longer offered and the raw enum is never shown.
    const onCreate = vi.fn().mockResolvedValue(undefined);
    await render(<CatalogWorkspace {...baseProps({ onCreate })} />);
    const detail = document.querySelector(".catalog-details")!;
    expect(detail.textContent).toContain("Takipsiz (eski kayıt)");
    expect(detail.textContent).not.toContain("Untracked");

    await click([...document.querySelectorAll("button")].find((button) => button.textContent?.includes("Ürün ekle"))!);
    const dialog = document.querySelector('[role="dialog"]')!;
    const stockMode = [...dialog.querySelectorAll("label")].find((label) => label.textContent?.startsWith("Stok modu"))!;
    const select = stockMode.querySelector("select")!;
    expect([...select.options].map((option) => option.textContent)).toEqual(["Miktar takipli", "Porsiyon takipli", "Reçeteden"]);
    expect(select.value).toBe("QuantityTracked");
    expect(stockMode.textContent).toContain("Siparişin onaylanması için ürünün stok eşlemesi olmalı.");

    const inputs = dialog.querySelectorAll<HTMLInputElement>("input");
    await fill(inputs[0], "SU-01");
    await fill(inputs[1], "Su");
    await fill(inputs[2], "20");
    await click([...dialog.querySelectorAll("button")].find((button) => button.textContent === "Kaydı oluştur")!);
    expect(dialog.textContent).toContain("KDV profili seçin.");
    expect(onCreate).not.toHaveBeenCalled();
    await choose(dialog, "KDV profili", "tax-1");
    await choose(dialog, "Kategori", "cat-1");
    await click([...dialog.querySelectorAll("button")].find((button) => button.textContent === "Kaydı oluştur")!);
    expect(onCreate).toHaveBeenCalledWith({ kind: "products", value: expect.objectContaining({ sku: "SU-01", stockMode: "QuantityTracked", taxProfileId: "tax-1", categoryId: "cat-1" }) });
  });

  it("shows product type, tax profile and price type in Turkish instead of raw values", async () => {
    // V1-RMD-438: the product subtitle showed the raw ProductType enum, the detail showed the tax profile id and the
    // price subtitle showed the raw PriceType enum with a product id fragment.
    await render(<CatalogWorkspace {...baseProps()} />);
    const row = document.querySelector(".catalog-row")!;
    expect(row.textContent).toContain("ESP-01 · Menü ürünü");
    const detail = document.querySelector(".catalog-details")!;
    expect(detail.textContent).toContain("KDV %10");
    expect(document.body.textContent).not.toContain("MenuItem");
    expect(detail.textContent).not.toContain("tax-1");

    await click([...document.querySelectorAll(".catalog-workspace__tabs button")].find((button) => button.textContent?.includes("Fiyatlar"))!);
    expect(document.body.textContent).toContain("Satış fiyatı · Espresso");
    expect(document.body.textContent).not.toContain("SalePrice");
  });

  it("creates a modifier group — the create surface that was entirely unreachable before this fix", async () => {
    // Found by an independent audit (2026-09-07): an operator could create
    // individual modifiers via this same screen but never the group their
    // mandatory ModifierGroupId must reference — this tab/form did not
    // exist at all.
    const onCreate = vi.fn().mockResolvedValue(undefined);
    await render(<CatalogWorkspace {...baseProps({ onCreate })} />);
    await click([...document.querySelectorAll(".catalog-workspace__tabs button")].find((button) => button.textContent?.includes("Modifikatör grupları"))!);
    await click([...document.querySelectorAll("button")].find((button) => button.textContent?.includes("Modifikatör grubu ekle"))!);
    const dialog = document.querySelector('[role="dialog"]')!;
    const inputs = dialog.querySelectorAll<HTMLInputElement>("input");
    await fill(inputs[0], "SIZE");
    await fill(inputs[1], "Boy seçimi");
    await click([...dialog.querySelectorAll("button")].find((button) => button.textContent === "Kaydı oluştur")!);

    expect(onCreate).toHaveBeenCalledWith({
      kind: "modifierGroups",
      value: expect.objectContaining({
        code: "SIZE", name: "Boy seçimi", selectionType: "SelectOne", minSelections: 0, maxSelections: 1,
      }),
    });
  });

  it.each([
    ["loading", "Katalog yükleniyor"], ["busy", "Katalog kaydediliyor"], ["offline", "Bağlantı yok"],
    ["unauthorized", "Yönetici oturumu gerekli"], ["error", "Katalog alınamadı"], ["stale", "Katalog güncel değil"], ["conflict", "Katalog çakışması"],
  ] as const)("shows bounded %s state", async (state, title) => {
    await render(<CatalogWorkspace {...baseProps({ state, errorMessage: "API kapalı" })} />);
    expect(document.body.textContent).toContain(title);
  });

  it("edits a product's identity and tax profile without asking for price or stock mode", async () => {
    const onUpdateProduct = vi.fn().mockResolvedValue(undefined);
    const withDetails = { ...data, products: [{ ...data.products[0], description: "Sıcak içecek", displayOrder: 5 }] };
    await render(<CatalogWorkspace {...baseProps({ onUpdateProduct, data: withDetails })} />);
    await click([...document.querySelectorAll(".catalog-details button")].find((button) => button.textContent === "Ürünü düzenle")!);
    const dialog = document.querySelector('[role="dialog"]')!;
    expect(dialog.textContent).toContain("Ürünü düzenle");
    expect(dialog.textContent).not.toContain("Satış fiyatı");
    expect(dialog.textContent).not.toContain("Stok modu");
    const inputs = dialog.querySelectorAll<HTMLInputElement>("input");
    expect(inputs[0].value).toBe("ESP-01");
    await fill(inputs[1], "Çift espresso");
    await click([...dialog.querySelectorAll("button")].find((button) => button.textContent === "Kaydet")!);
    expect(onUpdateProduct).toHaveBeenCalledWith("product-1", expect.objectContaining({ sku: "ESP-01", name: "Çift espresso", taxProfileId: "tax-1", categoryId: "cat-1", description: "Sıcak içecek", displayOrder: 5 }));
  });

  it("keeps the edit dialog open with the server's message when the update is refused", async () => {
    const onUpdateProduct = vi.fn().mockRejectedValue(new ApiError(409, "DUPLICATE_SKU", "Bu SKU zaten kullanılıyor."));
    await render(<CatalogWorkspace {...baseProps({ onUpdateProduct })} />);
    await click([...document.querySelectorAll(".catalog-details button")].find((button) => button.textContent === "Ürünü düzenle")!);
    const dialog = document.querySelector('[role="dialog"]')!;
    await click([...dialog.querySelectorAll("button")].find((button) => button.textContent === "Kaydet")!);
    expect(document.querySelector('[role="dialog"]')!.textContent).toContain("Bu SKU zaten kullanılıyor.");
  });

  it("offers no product edit without the update capability", async () => {
    await render(<CatalogWorkspace {...baseProps()} />);
    expect(document.querySelector(".catalog-details")!.textContent).not.toContain("Ürünü düzenle");
  });

  it("lists the groups bound to a product and assigns another one", async () => {
    const withGroups = {
      ...data,
      modifierGroups: [...data.modifierGroups, { ...data.modifierGroups[0], id: "group-2", code: "SYRUP", name: "Şurup" }],
      assignments: [{ id: "assignment-1", productId: "product-1", modifierGroupId: "group-1" }],
    };
    const onCreate = vi.fn().mockResolvedValue(undefined);
    await render(<CatalogWorkspace {...baseProps({ onCreate, data: withGroups })} />);
    const detail = document.querySelector(".catalog-details")!;
    expect(detail.textContent).toContain("BAĞLI MODİFİKATÖR GRUPLARI");
    expect(detail.textContent).toContain("Süt seçimi");
    const select = [...detail.querySelectorAll("label")].find((label) => label.textContent?.startsWith("Grup ata"))!.querySelector("select")!;
    expect([...select.options].map((option) => option.textContent)).toEqual(["Grup seçin", "Şurup"]);
    await choose(detail, "Grup ata", "group-2");
    await click([...detail.querySelectorAll("button")].find((button) => button.textContent === "Grubu ata")!);
    expect(onCreate).toHaveBeenCalledWith({ kind: "assignments", value: expect.objectContaining({ productId: "product-1", modifierGroupId: "group-2" }) });
  });

  it("offers no group assignment without manage permission", async () => {
    await render(<CatalogWorkspace {...baseProps({ canManage: false })} />);
    expect(document.querySelector(".catalog-details")!.textContent).not.toContain("Grubu ata");
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

  it("shows the backend's own Turkish message for a real CatalogApiError, not the generic fallback", async () => {
    // V1-RMD-368 (module-by-module UI audit, 2026-09-27): onSetAvailability's
    // REAL failures reach this component as catalogApi.ts's own
    // CatalogApiError, not the shared ApiError the V1-RMD-114 test above
    // checks - that test's mock (`new ApiError(...)`) never exercised the
    // class production code actually throws, so this went unnoticed.
    const onSetAvailability = vi.fn().mockRejectedValue(new CatalogApiError(409, "PRODUCT_IN_ACTIVE_ORDER", "Bu ürün açık bir siparişte olduğu için kaldırılamıyor."));
    await render(<CatalogWorkspace {...baseProps({ onSetAvailability })} />);
    await click([...document.querySelectorAll("button")].find((button) => button.textContent?.includes("Menüden kaldır"))!);
    expect(document.body.textContent).toContain("Bu ürün açık bir siparişte olduğu için kaldırılamıyor.");
  });

  it("has no critical or serious axe violations", async () => {
    await render(<CatalogWorkspace {...baseProps()} />);
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
