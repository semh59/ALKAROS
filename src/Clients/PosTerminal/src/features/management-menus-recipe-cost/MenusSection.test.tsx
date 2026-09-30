// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { type MenusClient } from "./api";
import { MenusSection } from "./index";
import { alertText, buttons, press, render, submit, type, unmount } from "../management/testKit";
import { ManagementApiError } from "../management/http";

const dailyItem = { id: "di1", productNameSnapshot: "Mercimek çorbası", price: 45, plannedPortions: 20, preparedPortions: 5, availablePortions: 0, isOutOfStock: true, isActive: true };
const daily = (status: string) => ({ menu: { id: "dm1", businessDate: "2026-09-30", status, note: null }, items: [dailyItem] });

function fakeClient(status: string | null = "Open", overrides: Partial<MenusClient> = {}): MenusClient {
  return {
    listMenus: vi.fn().mockResolvedValue([{ id: "m1", code: "ANA", name: "Ana menü", isActive: true }]),
    getMenu: vi.fn().mockResolvedValue({
      menu: { id: "m1", code: "ANA", name: "Ana menü", isActive: true },
      items: [{ menuItemId: "mi1", productId: "p1", productName: "Kuru fasulye", isProductActiveInCatalog: false, displayOrder: 1, isActive: true }],
    }),
    createMenu: vi.fn().mockResolvedValue(undefined),
    setMenuActive: vi.fn().mockResolvedValue(undefined),
    addMenuItem: vi.fn().mockResolvedValue(undefined),
    setMenuItem: vi.fn().mockResolvedValue(undefined),
    getDailyMenu: vi.fn().mockResolvedValue(status === null ? null : daily(status)),
    createDailyMenu: vi.fn().mockResolvedValue({ id: "dm1", businessDate: "2026-09-30", status: "Draft", note: null }),
    openDailyMenu: vi.fn().mockResolvedValue(undefined),
    closeDailyMenu: vi.fn().mockResolvedValue(undefined),
    addDailyMenuItem: vi.fn().mockResolvedValue(undefined),
    setDailyItemPrice: vi.fn().mockResolvedValue(undefined),
    setDailyItemPortions: vi.fn().mockResolvedValue(undefined),
    setDailyItemActive: vi.fn().mockResolvedValue(undefined),
    listProducts: vi.fn().mockResolvedValue([{ id: "p1", name: "Kuru fasulye" }]),
    listRecipeVersions: vi.fn().mockResolvedValue([
      { id: "v1", label: "Çorba · sürüm 1", status: "Active" },
      { id: "v2", label: "Çorba · sürüm 2", status: "Draft" },
    ]),
    ...overrides,
  };
}

const none = new Set<string>();

describe("menus section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("the shell lists the menus tab only for a session holding menu.manage", async () => {
    await render(<ManagementArea capabilities={new Set(["reports.view", "inventory.manage"])} />);
    expect(buttons()).not.toContain("Menüler");
    await render(<ManagementArea capabilities={new Set(["reports.view", "menu.manage"])} />);
    expect(buttons()).toContain("Menüler");
  });

  it("lists menus with Turkish states and flips the active flag with the current name", async () => {
    const client = fakeClient();
    await render(<MenusSection capabilities={none} client={client} />);
    expect(document.body.textContent).toContain("Ana menü");
    await press("Pasifleştir");
    expect(client.setMenuActive).toHaveBeenCalledWith(expect.objectContaining({ id: "m1", name: "Ana menü" }), false);
  });

  it("shows the menu composition and flags a product that is inactive in the catalogue", async () => {
    const client = fakeClient();
    await render(<MenusSection capabilities={none} client={client} />);
    await press("Kalemleri göster");
    expect(client.getMenu).toHaveBeenCalledWith("m1");
    expect(document.body.textContent).toContain("Kuru fasulye (Katalogda pasif)");
    await type("m-product", "p1");
    await submit("Menüye ürün ekle");
    expect(client.addMenuItem).toHaveBeenCalledWith("m1", "p1", 0);
  });

  it("rejects a non-numeric order without calling the server", async () => {
    const client = fakeClient();
    await render(<MenusSection capabilities={none} client={client} />);
    await press("Kalemleri göster");
    await type("m-order", "abc");
    await submit("Menüye ürün ekle");
    expect(alertText()).toBe("Geçerli bir sayı girin.");
    expect(client.addMenuItem).not.toHaveBeenCalled();
  });

  it("creates a menu from a code and a name", async () => {
    const client = fakeClient();
    await render(<MenusSection capabilities={none} client={client} />);
    await type("n-code", "AKS");
    await type("n-name", "Akşam");
    await submit("Yeni menü");
    expect(client.createMenu).toHaveBeenCalledWith("AKS", "Akşam");
  });

  it("with no daily menu it offers to create one and then reloads", async () => {
    const client = fakeClient(null);
    await render(<MenusSection capabilities={none} client={client} />);
    expect(document.body.textContent).toContain("Bu tarih için günün menüsü yok.");
    await type("d-note", "Hafta sonu");
    await submit("Günün menüsünü oluştur");
    expect(client.createDailyMenu).toHaveBeenCalledWith(expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/), "Hafta sonu");
    expect(client.getDailyMenu).toHaveBeenCalledTimes(2);
  });

  it("offers open for a draft, close for an open menu and no edits for a closed one", async () => {
    await render(<MenusSection capabilities={none} client={fakeClient("Draft")} />);
    expect(buttons()).toContain("Menüyü aç");
    expect(buttons()).not.toContain("Menüyü kapat");
    await render(<MenusSection capabilities={none} client={fakeClient("Open")} />);
    expect(buttons()).toContain("Menüyü kapat");
    await render(<MenusSection capabilities={none} client={fakeClient("Closed")} />);
    for (const forbidden of ["Menüyü aç", "Menüyü kapat", "Fiyatı değiştir", "Porsiyonu değiştir"]) expect(buttons()).not.toContain(forbidden);
    expect(document.querySelector('form[aria-label="Güne ürün ekle"]')).toBeNull();
  });

  it("shows Turkish status and stock wording, never a raw enum", async () => {
    await render(<MenusSection capabilities={none} client={fakeClient("PartiallyConsumed")} />);
    expect(document.body.textContent).toContain("Kısmen tüketildi");
    expect(document.body.textContent).toContain("Tükendi");
    expect(document.body.textContent).not.toContain("PartiallyConsumed");
    expect(buttons()).toContain("Menüyü kapat");
  });

  it("changes a price only for a valid number", async () => {
    const client = fakeClient();
    await render(<MenusSection capabilities={none} client={client} />);
    await press("Fiyatı değiştir");
    await type("d-edit", "abc");
    await submit("Mercimek çorbası");
    expect(alertText()).toBe("Geçerli bir sayı girin.");
    expect(client.setDailyItemPrice).not.toHaveBeenCalled();
    await type("d-edit", "52,5");
    await submit("Mercimek çorbası");
    expect(client.setDailyItemPrice).toHaveBeenCalledWith("di1", 52.5);
  });

  it("changes planned portions", async () => {
    const client = fakeClient();
    await render(<MenusSection capabilities={none} client={client} />);
    await press("Porsiyonu değiştir");
    await type("d-edit", "30");
    await submit("Mercimek çorbası");
    expect(client.setDailyItemPortions).toHaveBeenCalledWith("di1", 30);
  });

  it("adds a daily item with the catalogue price when the price is blank and offers only active recipe versions", async () => {
    const client = fakeClient();
    await render(<MenusSection capabilities={none} client={client} />);
    const recipeOptions = [...document.querySelectorAll("#i-recipe option")].map((option) => option.textContent);
    expect(recipeOptions).toEqual(["Yok", "Çorba · sürüm 1"]);
    await type("i-product", "p1");
    await type("i-planned", "12");
    await type("i-recipe", "v1");
    await submit("Güne ürün ekle");
    expect(client.addDailyMenuItem).toHaveBeenCalledWith("dm1", { productId: "p1", price: null, plannedPortions: 12, recipeVersionId: "v1" });
  });

  it("rejects an invalid planned portion count when adding a daily item", async () => {
    const client = fakeClient();
    await render(<MenusSection capabilities={none} client={client} />);
    await type("i-product", "p1");
    await submit("Güne ürün ekle");
    expect(alertText()).toBe("Geçerli bir sayı girin.");
    expect(client.addDailyMenuItem).not.toHaveBeenCalled();
  });

  it("the server's Turkish reason is shown as is", async () => {
    const client = fakeClient("Draft", { openDailyMenu: vi.fn().mockRejectedValue(new ManagementApiError(409, "X", "Menüde ürün olmadan açılamaz.")) });
    await render(<MenusSection capabilities={none} client={client} />);
    await press("Menüyü aç");
    expect(alertText()).toBe("Menüde ürün olmadan açılamaz.");
  });
});
