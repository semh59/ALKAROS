import type { CriticalStockReport, StockItem, StockLocation, VarianceReport } from "./models";

export class StockApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) { super(message); }
}

const fallbackByStatus = (status: number): string =>
  status === 401 ? "Oturum sona erdi; yeniden giriş yapın."
    : status === 403 ? "Bu işlem için yetkiniz yok."
    : status === 409 ? "Kayıt başka biri tarafından değiştirildi; yenileyip tekrar deneyin."
    : "İşlem tamamlanamadı.";

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
  const prefix = "/api/v1/management/inventory";

  async function call(path: string, init: RequestInit = {}): Promise<Response> {
    let response: Response;
    try {
      response = await fetcher(`${prefix}${path}`, {
        ...init,
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID() },
        signal: AbortSignal.timeout(8_000),
      });
    } catch {
      throw new StockApiError(0, "NETWORK_UNAVAILABLE", "Sunucuya ulaşılamadı. Bağlantıyı kontrol edip tekrar deneyin.");
    }
    if (response.ok) return response;
    const body = await response.json().catch(() => undefined) as { error?: { code?: string; message?: string } } | undefined;
    // The server's own Turkish reason is shown as is; only a missing one falls back to a generic Turkish line.
    throw new StockApiError(response.status, body?.error?.code ?? "REQUEST_FAILED", body?.error?.message ?? fallbackByStatus(response.status));
  }

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
