// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { MenuApiError, type CostClient } from "./api";
import { RecipeCostSection } from "./index";
import { alertText, buttons, press, render, submit, type, unmount } from "./testKit";

const snapshot = {
  id: "s1", costBasisDate: "2026-09-30", calculatedCost: 200, costPerPortion: 10, currency: "TRY",
  items: [{ stockItemId: "i1", effectiveNativeQuantity: 2, nativeUnitCode: "kg", unitCost: 60, lineCost: 120 }],
};

function fakeClient(overrides: Partial<CostClient> = {}): CostClient {
  return {
    listRecipeVersions: vi.fn().mockResolvedValue([{ id: "v1", label: "Çorba · sürüm 1", status: "Active" }]),
    getEffective: vi.fn().mockResolvedValue(snapshot),
    calculate: vi.fn().mockResolvedValue(snapshot),
    listStockItemNames: vi.fn().mockResolvedValue(new Map([["i1", "Mercimek"]])),
    ...overrides,
  };
}

const none = new Set<string>();

describe("recipe cost section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("the shell lists the recipe cost tab only for a session holding inventory.manage", async () => {
    await render(<ManagementArea capabilities={new Set(["reports.view", "menu.manage"])} />);
    expect(buttons()).not.toContain("Tarif maliyeti");
    await render(<ManagementArea capabilities={new Set(["reports.view", "inventory.manage"])} />);
    expect(buttons()).toContain("Tarif maliyeti");
  });

  it("shows nothing to calculate until a version is picked", async () => {
    await render(<RecipeCostSection capabilities={none} client={fakeClient()} />);
    const disabled = (text: string) => ([...document.querySelectorAll("button")].find((button) => button.textContent === text) as HTMLButtonElement).disabled;
    expect(disabled("Geçerli maliyeti göster")).toBe(true);
    expect(disabled("Bugünkü maliyeti hesapla")).toBe(true);
    expect(document.body.textContent).toContain("Çorba · sürüm 1 (Etkin)");
  });

  it("shows the effective cost with the ingredient names", async () => {
    const client = fakeClient();
    await render(<RecipeCostSection capabilities={none} client={client} />);
    await type("c-version", "v1");
    await type("c-date", "2026-09-30");
    await submit("Tarif maliyeti");
    expect(client.getEffective).toHaveBeenCalledWith("v1", "2026-09-30");
    expect(document.body.textContent).toContain("Mercimek");
    expect(document.body.textContent).toContain("2 kg");
  });

  it("explains when no cost is recorded for the date", async () => {
    await render(<RecipeCostSection capabilities={none} client={fakeClient({ getEffective: vi.fn().mockResolvedValue(null) })} />);
    await type("c-version", "v1");
    await submit("Tarif maliyeti");
    expect(document.body.textContent).toContain("Bu sürüm için seçilen tarihte kayıtlı bir maliyet yok.");
  });

  it("calculates the cost for today and shows the result", async () => {
    const client = fakeClient();
    await render(<RecipeCostSection capabilities={none} client={client} />);
    await type("c-version", "v1");
    await press("Bugünkü maliyeti hesapla");
    expect(client.calculate).toHaveBeenCalledWith("v1", expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/));
    expect(document.body.textContent).toContain("Maliyet hesaplandı.");
    expect(client.getEffective).toHaveBeenCalled();
  });

  it("shows the server's Turkish reason when the calculation is refused", async () => {
    const client = fakeClient({ calculate: vi.fn().mockRejectedValue(new MenuApiError(409, "X", "Malzeme için fiyat bulunamadı.")) });
    await render(<RecipeCostSection capabilities={none} client={client} />);
    await type("c-version", "v1");
    await press("Bugünkü maliyeti hesapla");
    expect(alertText()).toBe("Malzeme için fiyat bulunamadı.");
  });

  it("says so when there is no recipe version", async () => {
    await render(<RecipeCostSection capabilities={none} client={fakeClient({ listRecipeVersions: vi.fn().mockResolvedValue([]) })} />);
    expect(document.body.textContent).toContain("Tarif sürümü bulunamadı.");
  });
});
