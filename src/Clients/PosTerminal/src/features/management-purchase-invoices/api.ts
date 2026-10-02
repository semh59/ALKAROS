import { createRequester } from "../management/http";
import type { LookupItem, LookupLocation, PurchaseInvoice, PurchaseInvoiceSummary } from "./models";

export interface PurchaseInvoiceClient {
  importXml: (xml: string) => Promise<PurchaseInvoice>;
  list: (status: string) => Promise<readonly PurchaseInvoiceSummary[]>;
  get: (invoiceId: string) => Promise<PurchaseInvoice>;
  mapLine: (invoiceId: string, lineId: string, stockItemId: string, conversionFactor: number) => Promise<PurchaseInvoice>;
  approve: (invoiceId: string, locationId: string) => Promise<void>;
  reject: (invoiceId: string) => Promise<void>;
  listLocations: () => Promise<readonly LookupLocation[]>;
  listStockItems: () => Promise<readonly LookupItem[]>;
}

export function createPurchaseInvoiceClient(fetcher: typeof fetch = fetch): PurchaseInvoiceClient {
  const call = createRequester(fetcher, { timeoutMs: 20_000 });
  const send = (method: string, path: string, body: unknown) =>
    call(path, { method, body: JSON.stringify({ ...(body as object), idempotencyKey: crypto.randomUUID() }) });
  const json = async <T>(path: string): Promise<T> => (await call(path)).json() as Promise<T>;
  const base = "/purchasing/purchase-invoices";
  return {
    importXml: async (xml) => (await send("POST", `${base}/import`, { xml })).json() as Promise<PurchaseInvoice>,
    list: (status) => json(`${base}${status ? `?status=${status}` : ""}`),
    get: (invoiceId) => json(`${base}/${invoiceId}`),
    mapLine: async (invoiceId, lineId, stockItemId, conversionFactor) =>
      (await send("PUT", `${base}/${invoiceId}/lines/${lineId}/mapping`, { stockItemId, conversionFactor })).json() as Promise<PurchaseInvoice>,
    approve: async (invoiceId, locationId) => { await send("POST", `${base}/${invoiceId}/approve`, { locationId }); },
    reject: async (invoiceId) => { await send("POST", `${base}/${invoiceId}/reject`, {}); },
    listLocations: () => json("/inventory/stock-locations?activeOnly=true"),
    listStockItems: () => json("/inventory/stock-items?activeOnly=true"),
  };
}
