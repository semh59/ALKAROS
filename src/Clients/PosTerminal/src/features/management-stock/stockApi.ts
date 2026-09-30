import type { CriticalStockReport, StockItem, StockLocation, VarianceReport } from "./models";
import { createRequester } from "../management/http";

export interface NewStockItem { code: string; name: string; itemType: string; trackingUnitCode: string; defaultLocationId: string | null; reorderPoint: number | null }
export interface NewStockLocation { code: string; name: string; locationType: string }
export interface WasteInput { stockLocationId: string; wasteSource: string; quantity: number; unitCode: string; reason: string }

export interface StockClient {
  listItems: () => Promise<readonly StockItem[]>;
  listLocations: () => Promise<readonly StockLocation[]>;
  createItem: (input: NewStockItem) => Promise<void>;
  createLocation: (input: NewStockLocation) => Promise<void>;
  setReorderPoint: (itemId: string, value: number | null) => Promise<void>;
  recordCount: (itemId: string, stockLocationId: string, countedQuantity: number, notes: string) => Promise<void>;
  recordWaste: (itemId: string, input: WasteInput) => Promise<void>;
  getCriticalStock: () => Promise<CriticalStockReport>;
  getVariance: (from: string, to: string) => Promise<VarianceReport>;
}

export function createStockClient(fetcher: typeof fetch = fetch): StockClient {
  const call = createRequester(fetcher, { prefix: "/inventory" });

  const json = async <T>(path: string): Promise<T> => (await call(path)).json() as Promise<T>;
  const send = (method: string, path: string, body: unknown) => call(path, { method, body: JSON.stringify(body) }).then(() => undefined);

  return {
    listItems: () => json("/stock-items"),
    listLocations: () => json("/stock-locations"),
    createItem: (input) => send("POST", "/stock-items", input),
    createLocation: (input) => send("POST", "/stock-locations", input),
    setReorderPoint: (itemId, value) => send("PUT", `/stock-items/${itemId}/reorder-point`, { reorderPoint: value }),
    recordCount: (itemId, stockLocationId, countedQuantity, notes) =>
      send("POST", `/stock-items/${itemId}/physical-counts`, { stockLocationId, countedQuantity, notes: notes.trim() || null }),
    recordWaste: (itemId, input) => send("POST", `/stock-items/${itemId}/waste`, { ...input, idempotencyKey: crypto.randomUUID() }),
    getCriticalStock: () => json("/reports/critical-stock"),
    getVariance: (from, to) => json(`/reports/actual-vs-theoretical?from=${encodeURIComponent(`${from}T00:00:00Z`)}&to=${encodeURIComponent(`${to}T23:59:59Z`)}`),
  };
}
