// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { ManagementApiError } from "../management/http";
import { alertText, buttons, press, render, submit, type, unmount } from "../management/testKit";
import type { RecipesClient } from "./api";
import { RecipesSection } from "./index";
import type { RecipeVersion } from "./models";

const version = (overrides: Partial<RecipeVersion> = {}): RecipeVersion => ({
  id: "v1", recipeId: "r1", versionNumber: 1, status: "Draft", yieldQuantity: 4, yieldUnitCode: "porsiyon", preparationMinutes: 0, isLocked: false,
  ingredients: [{ ingredientItemId: "s1", quantity: 200, unitCode: "g", lossPercentage: 5, sortOrder: 0, notes: null }], ...overrides,
});

function fakeClient(versions: RecipeVersion[] = [version()], overrides: Partial<RecipesClient> = {}): RecipesClient {
  return {
    listRecipes: vi.fn().mockResolvedValue([{ id: "r1", code: "PIZ", name: "Pizza", description: null }]),
    createRecipe: vi.fn().mockResolvedValue(undefined),
    listVersions: vi.fn().mockResolvedValue(versions),
    createFirstVersion: vi.fn().mockResolvedValue(undefined),
    createNextVersion: vi.fn().mockResolvedValue(undefined),
    addIngredient: vi.fn().mockResolvedValue(undefined),
    removeIngredient: vi.fn().mockResolvedValue(undefined),
    activate: vi.fn().mockResolvedValue(undefined),
    lock: vi.fn().mockResolvedValue(undefined),
    listConversions: vi.fn().mockResolvedValue([{ id: "c1", fromUnitCode: "kasa", toUnitCode: "adet", factor: 12 }]),
    createConversion: vi.fn().mockResolvedValue(undefined),
    listStockItems: vi.fn().mockResolvedValue([{ id: "s1", name: "Un", trackingUnitCode: "g" }, { id: "s2", name: "Süt", trackingUnitCode: "ml" }]),
    listProducts: vi.fn().mockResolvedValue([{ id: "p1", name: "Margarita" }]),
    getMapping: vi.fn().mockResolvedValue(null),
    assignMapping: vi.fn().mockResolvedValue(undefined),
    removeMapping: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  };
}

const none = new Set<string>();
async function open(client: RecipesClient) {
  await render(<RecipesSection capabilities={none} client={client} />);
  await press("Sürümleri göster");
}

describe("recipes section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("the shell lists the recipes tab only for a session holding inventory.manage", async () => {
    await render(<ManagementArea capabilities={new Set(["reports.view", "menu.manage"])} />);
    expect(buttons()).not.toContain("Tarifler");
    await render(<ManagementArea capabilities={new Set(["reports.view", "inventory.manage"])} />);
    expect(buttons()).toContain("Tarifler");
  });

  it("creates a recipe from a code and a name", async () => {
    const client = fakeClient();
    await render(<RecipesSection capabilities={none} client={client} />);
    await type("r-code", "PIZ2");
    await type("r-name", "Pizza 2");
    await submit("Yeni tarif");
    expect(client.createRecipe).toHaveBeenCalledWith("PIZ2", "Pizza 2", "");
  });

  it("shows Turkish version states and offers activate for a draft, lock for an active unlocked version only", async () => {
    const client = fakeClient([version({ versionNumber: 4 }), version({ id: "v2", versionNumber: 2, status: "Active" }), version({ id: "v3", versionNumber: 3, status: "Active", isLocked: true })]);
    await open(client);
    expect(document.body.textContent).toContain("Taslak");
    expect(document.body.textContent).toContain("Etkin");
    expect(document.body.textContent).not.toContain("Draft");
    expect(buttons().filter((text) => text === "Etkinleştir")).toHaveLength(1);
    expect(buttons().filter((text) => text === "Kilitle")).toHaveLength(1);
    await press("Etkinleştir");
    expect(client.activate).toHaveBeenCalledWith("r1", 4);
    await press("Kilitle");
    expect(client.lock).toHaveBeenCalledWith("v2");
  });

  it("with no version it asks for the yield and rejects a non-numeric quantity without calling the server", async () => {
    const client = fakeClient([]);
    await open(client);
    await type("v-yield", "abc");
    await submit("İlk sürümü oluştur");
    expect(alertText()).toBe("Geçerli bir sayı girin.");
    expect(client.createFirstVersion).not.toHaveBeenCalled();
    await type("v-yield", "4,5");
    await type("v-unit", "porsiyon");
    await submit("İlk sürümü oluştur");
    expect(client.createFirstVersion).toHaveBeenCalledWith("r1", 4.5, "porsiyon");
  });

  it("adds an ingredient with the stock item's own unit and removes one only on a draft", async () => {
    const client = fakeClient();
    await open(client);
    await press("Malzemeleri göster");
    expect(document.body.textContent).toContain("Un");
    await type("i-item", "s2");
    expect((document.getElementById("i-unit") as HTMLInputElement).value).toBe("ml");
    await type("i-quantity", "250");
    await submit("Malzeme ekle");
    expect(client.addIngredient).toHaveBeenCalledWith("v1", { ingredientItemId: "s2", quantity: 250, unitCode: "ml", lossPercentage: 0 });
    await press("Çıkar");
    expect(client.removeIngredient).toHaveBeenCalledWith("v1", "s1");
  });

  it("offers no ingredient editing on an active version", async () => {
    await open(fakeClient([version({ status: "Active" })]));
    await press("Malzemeleri göster");
    expect(buttons()).not.toContain("Çıkar");
    expect(document.querySelector('form[aria-label="Malzeme ekle"]')).toBeNull();
  });

  it("maps a product to a recipe and removes an existing mapping", async () => {
    const client = fakeClient();
    await render(<RecipesSection capabilities={none} client={client} />);
    await type("m-product", "p1");
    expect(document.body.textContent).toContain("Bu ürün için tarif eşlemesi yok.");
    await type("m-recipe", "r1");
    await submit("Eşle");
    expect(client.assignMapping).toHaveBeenCalledWith("p1", "r1");

    const mapped = fakeClient([version()], { getMapping: vi.fn().mockResolvedValue({ productId: "p1", recipeId: "r1", isActive: true, notes: null }) });
    await render(<RecipesSection capabilities={none} client={mapped} />);
    await type("m-product", "p1");
    expect(document.body.textContent).toContain("Eşlenen tarif: Pizza");
    await press("Eşlemeyi kaldır");
    expect(mapped.removeMapping).toHaveBeenCalledWith("p1");
  });

  it("creates a unit conversion only for a valid factor", async () => {
    const client = fakeClient();
    await render(<RecipesSection capabilities={none} client={client} />);
    expect(document.body.textContent).toContain("kasa");
    await type("c-from", "koli");
    await type("c-to", "adet");
    await type("c-factor", "x");
    await submit("Yeni dönüşüm");
    expect(client.createConversion).not.toHaveBeenCalled();
    await type("c-factor", "6");
    await submit("Yeni dönüşüm");
    expect(client.createConversion).toHaveBeenCalledWith("koli", "adet", 6);
  });

  it("shows the server's Turkish refusal and a fixed sentence for an unknown failure", async () => {
    const refused = fakeClient([version()], { activate: vi.fn().mockRejectedValue(new ManagementApiError(409, "CONFLICT", "Sürüm başka biri tarafından değiştirildi.")) });
    await open(refused);
    await press("Etkinleştir");
    expect(alertText()).toBe("Sürüm başka biri tarafından değiştirildi.");

    const broken = fakeClient([version()], { activate: vi.fn().mockRejectedValue(new TypeError("Failed to fetch")) });
    await open(broken);
    await press("Etkinleştir");
    expect(alertText()).toBe("İşlem tamamlanamadı.");
  });
});
