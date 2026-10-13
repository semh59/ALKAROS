import type { CriticalStockReport, MappingTarget, StockItem, StockLocation, StockMapping, TargetOption, VarianceReport, WasteRecord } from "./models";
import { createRequester } from "../management/http";

export interface NewStockItem { code: string; name: string; itemType: string; trackingUnitCode: string; defaultLocationId: string | null; reorderPoint: number | null }
export interface NewStockLocation { code: string; name: string; locationType: string }
export interface WasteInput { stockLocationId: string; wasteSource: string; quantity: number; unitCode: string; reason: string }

export interface MappingInput { stockItemId: string; quantityMultiplier: number; notes: string | null }

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
  listTargets: (kind: MappingTarget) => Promise<readonly TargetOption[]>;
  listMappings: (kind: MappingTarget, targetId: string) => Promise<readonly StockMapping[]>;
  assignMapping: (kind: MappingTarget, targetId: string, input: MappingInput) => Promise<void>;
  removeMapping: (kind: MappingTarget, targetId: string, stockItemId: string) => Promise<void>;
  listWaste: (wasteSource: string, sourceReferenceId: string) => Promise<readonly WasteRecord[]>;
}

const mappingBase = (kind: MappingTarget, targetId: string) => `/${kind === "product" ? "products" : "modifiers"}/${targetId}/stock-mappings`;

export function createStockClient(fetcher: typeof fetch = fetch): StockClient {
  const call = createRequester(fetcher, { prefix: "/inventory" });
  const catalog = createRequester(fetcher);

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
    async listTargets(kind) {
      const response = await catalog(`/catalog/${kind === "product" ? "products" : "modifiers"}?limit=100`);
      const result = await response.json() as { items: TargetOption[] } | TargetOption[];
      return Array.isArray(result) ? result : result.items;
    },
    async listMappings(kind, targetId) {
      const rows = await json<readonly (Omit<StockMapping, "stockItemName"> & { stockItemName?: string })[]>(mappingBase(kind, targetId));
      return rows.map((row) => ({ ...row, stockItemName: row.stockItemName ?? null }));
    },
    assignMapping: (kind, targetId, input) => send("POST", mappingBase(kind, targetId), input),
    removeMapping: (kind, targetId, stockItemId) => call(`${mappingBase(kind, targetId)}/${stockItemId}`, { method: "DELETE" }).then(() => undefined),
    listWaste: (wasteSource, sourceReferenceId) =>
      json(`/waste?wasteSource=${encodeURIComponent(wasteSource)}&sourceReferenceId=${encodeURIComponent(sourceReferenceId)}`),
    getVariance: (from, to) => json(`/reports/actual-vs-theoretical?from=${encodeURIComponent(`${from}T00:00:00Z`)}&to=${encodeURIComponent(`${to}T23:59:59Z`)}`),
  };
}
