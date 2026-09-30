// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { type ProductionClient } from "./api";
import { ProductionSection } from "./index";
import { alertText, buttons, press, render, submit, type, unmount } from "../management/testKit";
import { ManagementApiError } from "../management/http";

const batch = (status: string) => ({
  id: "b1", batchNumber: "PRT-1", recipeVersionId: "v1", status, plannedQuantity: 20, actualQuantity: 0, portionUnitCode: "portion", cancellationReason: null,
});

function fakeClient(status = "InProgress", overrides: Partial<ProductionClient> = {}): ProductionClient {
  return {
    listBatches: vi.fn().mockResolvedValue([batch(status)]),
    createBatch: vi.fn().mockResolvedValue(undefined),
    startBatch: vi.fn().mockResolvedValue(undefined),
    completeBatch: vi.fn().mockResolvedValue(undefined),
    cancelBatch: vi.fn().mockResolvedValue(undefined),
    listRecipeVersions: vi.fn().mockResolvedValue([{ id: "v1", label: "Mercimek çorbası · sürüm 2" }]),
    listLocations: vi.fn().mockResolvedValue([{ id: "l1", name: "Mutfak" }, { id: "l2", name: "Soğuk oda" }]),
    listStockItems: vi.fn().mockResolvedValue([{ id: "i1", name: "Çorba", trackingUnitCode: "portion" }]),
    ...overrides,
  };
}

const none = new Set<string>();

describe("production section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("the shell lists the production tab only for a session holding production.manage", async () => {
    await render(<ManagementArea capabilities={new Set(["reports.view", "purchasing.manage"])} />);
    expect(buttons()).not.toContain("Üretim");
    await render(<ManagementArea capabilities={new Set(["reports.view", "production.manage"])} />);
    expect(buttons()).toContain("Üretim");
  });

  it("offers start for a planned batch, complete for a batch in production and nothing for a finished one", async () => {
    await render(<ProductionSection capabilities={none} client={fakeClient("Planned")} />);
    expect(buttons()).toEqual(expect.arrayContaining(["Başlat", "İptal et"]));
    expect(buttons()).not.toContain("Tamamla");
    await render(<ProductionSection capabilities={none} client={fakeClient("InProgress")} />);
    expect(buttons()).toContain("Tamamla");
    expect(buttons()).not.toContain("Başlat");
    await render(<ProductionSection capabilities={none} client={fakeClient("Completed")} />);
    for (const forbidden of ["Başlat", "Tamamla", "İptal et"]) expect(buttons()).not.toContain(forbidden);
  });

  it("shows Turkish statuses and no raw enum value", async () => {
    await render(<ProductionSection capabilities={none} client={fakeClient("InProgress")} />);
    expect(document.body.textContent).toContain("Üretimde");
    expect(document.body.textContent).not.toContain("InProgress");
  });

  it("completing a batch needs a number and a source location, then sends both", async () => {
    const client = fakeClient();
    await render(<ProductionSection capabilities={none} client={client} />);
    await press("Tamamla");
    await type("p-actual", "x");
    await submit("PRT-1");
    expect(alertText()).toBe("Geçerli bir sayı girin.");
    await type("p-actual", "18,5");
    await submit("PRT-1");
    expect(alertText()).toBe("Bir konum seçin.");
    expect(client.completeBatch).not.toHaveBeenCalled();
    await type("p-source", "l1");
    await type("p-output", "i1");
    await submit("PRT-1");
    expect(client.completeBatch).toHaveBeenCalledWith("b1", { actualQuantity: 18.5, sourceLocationId: "l1", destinationLocationId: null, outputStockItemId: "i1" });
  });

  it("a batch is created from an active recipe version", async () => {
    const client = fakeClient();
    await render(<ProductionSection capabilities={none} client={client} />);
    await type("b-number", "PRT-9");
    await type("b-version", "v1");
    await type("b-planned", "30");
    await submit("Yeni üretim partisi");
    expect(client.createBatch).toHaveBeenCalledWith({ batchNumber: "PRT-9", recipeVersionId: "v1", plannedQuantity: 30, portionUnitCode: "portion" });
  });

  it("with no active recipe version it explains why a batch cannot be created", async () => {
    await render(<ProductionSection capabilities={none} client={fakeClient("Planned", { listRecipeVersions: vi.fn().mockResolvedValue([]) })} />);
    expect(document.body.textContent).toContain("Etkin tarif sürümü bulunamadı");
    expect(document.querySelector('form[aria-label="Yeni üretim partisi"]')).toBeNull();
  });

  it("the server's Turkish reason is shown as is", async () => {
    const client = fakeClient("Planned", { startBatch: vi.fn().mockRejectedValue(new ManagementApiError(409, "X", "Parti zaten başlatılmış.")) });
    await render(<ProductionSection capabilities={none} client={client} />);
    await press("Başlat");
    expect(alertText()).toBe("Parti zaten başlatılmış.");
  });
});
