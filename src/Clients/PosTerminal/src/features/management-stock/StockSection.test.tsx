// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { StockApiError, type StockClient } from "./stockApi";
import { StockSection } from "./index";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

function fakeClient(overrides: Partial<StockClient> = {}): StockClient {
  return {
    listItems: vi.fn().mockResolvedValue([
      { id: "i1", code: "UN-01", name: "Un", itemType: "RawMaterial", trackingUnitCode: "kg", defaultLocationId: "l1", isActive: true, reorderPoint: 5 },
    ]),
    listLocations: vi.fn().mockResolvedValue([{ id: "l1", code: "DEPO", name: "Ana depo", locationType: "ColdStorage", isActive: true }]),
    createItem: vi.fn().mockResolvedValue(undefined),
    createLocation: vi.fn().mockResolvedValue(undefined),
    setReorderPoint: vi.fn().mockResolvedValue(undefined),
    recordCount: vi.fn().mockResolvedValue(undefined),
    recordWaste: vi.fn().mockResolvedValue(undefined),
    getCriticalStock: vi.fn().mockResolvedValue({
      items: [{ stockItemId: "i1", stockItemName: "Un", trackingUnitCode: "kg", locationName: "Ana depo", onHandQuantity: 3, availableQuantity: 2, criticalThreshold: 5, isCritical: true }],
      totalCriticalItemsCount: 1,
    }),
    getVariance: vi.fn().mockResolvedValue({
      items: [{ stockItemId: "i1", stockItemName: "Un", trackingUnitCode: "kg", locationName: "Ana depo", actualUsage: 10, theoreticalUsage: 8, varianceQuantity: 2, variancePercentage: 25 }],
      excludedForMissingCountsCount: 4,
    }),
    ...overrides,
  };
}

const manager = new Set(["reports.view", "inventory.manage"]);

describe("stock section", () => {
  let root: Root | null = null;
  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }
  const buttons = () => [...document.querySelectorAll("button")].map((button) => button.textContent);
  const press = async (text: string) => {
    const button = [...document.querySelectorAll("button")].find((candidate) => candidate.textContent === text)!;
    await act(async () => button.click());
  };
  const type = async (id: string, value: string) => {
    const input = document.getElementById(id) as HTMLInputElement | HTMLSelectElement;
    const proto = input instanceof HTMLSelectElement ? HTMLSelectElement.prototype : HTMLInputElement.prototype;
    await act(async () => {
      Object.getOwnPropertyDescriptor(proto, "value")!.set!.call(input, value);
      input.dispatchEvent(new Event(input instanceof HTMLSelectElement ? "change" : "input", { bubbles: true }));
    });
  };
  const submit = async (label: string) => {
    const form = document.querySelector(`form[aria-label="${label}"]`)!;
    await act(async () => { form.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true })); });
  };
  afterEach(async () => { if (root) await act(async () => root!.unmount()); root = null; vi.restoreAllMocks(); });

  it("a view-only session sees the reports and never reads or changes stock items", async () => {
    const client = fakeClient();
    await render(<StockSection client={client} capabilities={new Set(["reports.view"])} />);
    expect(document.body.textContent).toContain("Kritik stok");
    expect(document.body.textContent).toContain("Gerçek ve teorik kullanım farkı");
    expect(document.body.textContent).not.toContain("Stok kalemleri");
    expect(client.listItems).not.toHaveBeenCalled();
    for (const forbidden of ["Sayım gir", "Fire kaydet", "Sipariş noktası", "Ekle"]) expect(buttons()).not.toContain(forbidden);
  });

  it("a manager sees the item and location tools", async () => {
    await render(<StockSection client={fakeClient()} capabilities={manager} />);
    expect(buttons()).toEqual(expect.arrayContaining(["Sayım gir", "Fire kaydet", "Sipariş noktası", "Ekle"]));
    expect(document.body.textContent).toContain("Açılış veya kapanış sayımı eksik olduğu için dışarıda kalan kalem sayısı: 4");
  });

  it("shows Turkish labels and never a raw enum value", async () => {
    await render(<StockSection client={fakeClient()} capabilities={manager} />);
    const text = document.body.textContent ?? "";
    for (const raw of ["RawMaterial", "ColdStorage", "Spoilage"]) expect(text).not.toContain(raw);
    expect(text).toContain("Hammadde");
    expect(text).toContain("Soğuk hava deposu");
  });

  it("a count accepts a decimal comma and sends the chosen location", async () => {
    const client = fakeClient();
    await render(<StockSection client={client} capabilities={manager} />);
    await press("Sayım gir");
    await type("stock-quantity", "12,5");
    await submit("Un");
    expect(client.recordCount).toHaveBeenCalledWith("i1", "l1", 12.5, "");
  });

  it("waste uses the item's own unit and refuses a non-number without calling the server", async () => {
    const client = fakeClient();
    await render(<StockSection client={client} capabilities={manager} />);
    await press("Fire kaydet");
    await type("stock-quantity", "abc");
    await submit("Un");
    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Geçerli bir sayı girin.");
    expect(client.recordWaste).not.toHaveBeenCalled();
    await type("stock-quantity", "2");
    await type("stock-source", "Spoilage");
    await type("stock-reason", "Küflenmiş");
    await submit("Un");
    expect(client.recordWaste).toHaveBeenCalledWith("i1", { stockLocationId: "l1", wasteSource: "Spoilage", quantity: 2, unitCode: "kg", reason: "Küflenmiş" });
  });

  it("an empty reorder point clears the threshold", async () => {
    const client = fakeClient();
    await render(<StockSection client={client} capabilities={manager} />);
    await press("Sipariş noktası");
    await type("stock-reorder", "");
    await submit("Un");
    expect(client.setReorderPoint).toHaveBeenCalledWith("i1", null);
  });

  it("the server's Turkish reason is shown as is", async () => {
    const client = fakeClient({ recordWaste: vi.fn().mockRejectedValue(new StockApiError(409, "INSUFFICIENT_STOCK", "Rafta bu kadar stok yok.")) });
    await render(<StockSection client={client} capabilities={manager} />);
    await press("Fire kaydet");
    await type("stock-quantity", "99");
    await submit("Un");
    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Rafta bu kadar stok yok.");
  });

  it("a new item is created with the chosen type and an optional default location", async () => {
    const client = fakeClient();
    await render(<StockSection client={client} capabilities={manager} />);
    await type("n-code", "SUT-01");
    await type("n-name", "Süt");
    await type("n-type", "Portion");
    await type("n-unit", "lt");
    await submit("Yeni stok kalemi");
    expect(client.createItem).toHaveBeenCalledWith({ code: "SUT-01", name: "Süt", itemType: "Portion", trackingUnitCode: "lt", defaultLocationId: null, reorderPoint: null });
  });
});
