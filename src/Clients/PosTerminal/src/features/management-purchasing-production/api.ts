import type {
  Batch, BatchCompletion, LookupItem, LookupLocation, NewOrderLine, PurchaseOrder, ReceiptLine, Supplier, NewSupplier, RecipeVersionOption,
} from "./models";
import { createRequester } from "../management/http";

export interface PurchasingClient {
  listSuppliers: () => Promise<readonly Supplier[]>;
  createSupplier: (input: NewSupplier) => Promise<void>;
  setSupplierActive: (supplierId: string, active: boolean) => Promise<void>;
  listOrders: (status: string) => Promise<readonly PurchaseOrder[]>;
  createOrder: (input: { orderNumber: string; supplierId: string; destinationLocationId: string; lines: readonly NewOrderLine[]; notes: string }) => Promise<void>;
  submitOrder: (orderId: string) => Promise<void>;
  cancelOrder: (orderId: string) => Promise<void>;
  receiveGoods: (orderId: string, input: { receiptNumber: string; items: readonly ReceiptLine[]; isManagerApproved: boolean; notes: string }) => Promise<void>;
  listLocations: () => Promise<readonly LookupLocation[]>;
  listStockItems: () => Promise<readonly LookupItem[]>;
}

export function createPurchasingClient(fetcher: typeof fetch = fetch): PurchasingClient {
  const call = createRequester(fetcher);
  const json = async <T>(path: string): Promise<T> => (await call(path)).json() as Promise<T>;
  const post = (path: string, body: unknown = {}) => call(path, { method: "POST", body: JSON.stringify(body) }).then(() => undefined);
  return {
    listSuppliers: () => json("/purchasing/suppliers"),
    createSupplier: (input) => post("/purchasing/suppliers", input),
    setSupplierActive: (supplierId, active) => post(`/purchasing/suppliers/${supplierId}/${active ? "activate" : "deactivate"}`),
    listOrders: (status) => json(`/purchasing/purchase-orders${status ? `?status=${status}` : ""}`),
    createOrder: (input) => post("/purchasing/purchase-orders", { ...input, notes: input.notes.trim() || null }),
    submitOrder: (orderId) => post(`/purchasing/purchase-orders/${orderId}/submit`),
    cancelOrder: (orderId) => post(`/purchasing/purchase-orders/${orderId}/cancel`),
    receiveGoods: (orderId, input) => post(`/purchasing/purchase-orders/${orderId}/receipts`, {
      receiptNumber: input.receiptNumber, deliveredItems: input.items, isManagerApproved: input.isManagerApproved, notes: input.notes.trim() || null,
    }),
    listLocations: () => json("/inventory/stock-locations?activeOnly=true"),
    listStockItems: () => json("/inventory/stock-items?activeOnly=true"),
  };
}

export interface ProductionClient {
  listBatches: (status: string) => Promise<readonly Batch[]>;
  createBatch: (input: { batchNumber: string; recipeVersionId: string; plannedQuantity: number; portionUnitCode: string }) => Promise<void>;
  startBatch: (batchId: string) => Promise<void>;
  completeBatch: (batchId: string, input: BatchCompletion) => Promise<void>;
  cancelBatch: (batchId: string, reason: string) => Promise<void>;
  listRecipeVersions: () => Promise<readonly RecipeVersionOption[]>;
  listLocations: () => Promise<readonly LookupLocation[]>;
  listStockItems: () => Promise<readonly LookupItem[]>;
}

export function createProductionClient(fetcher: typeof fetch = fetch): ProductionClient {
  const call = createRequester(fetcher);
  const json = async <T>(path: string): Promise<T> => (await call(path)).json() as Promise<T>;
  const post = (path: string, body: unknown = {}) => call(path, { method: "POST", body: JSON.stringify(body) }).then(() => undefined);
  return {
    listBatches: (status) => json(`/production/batches${status ? `?status=${status}` : ""}`),
    createBatch: (input) => post("/production/batches", input),
    startBatch: (batchId) => post(`/production/batches/${batchId}/start`, { startedAt: null }),
    completeBatch: (batchId, input) => post(`/production/batches/${batchId}/complete`, input),
    cancelBatch: (batchId, reason) => post(`/production/batches/${batchId}/cancel`, { reason: reason.trim() || null }),
    async listRecipeVersions() {
      const recipes = await json<readonly { id: string; code: string; name: string }[]>("/recipes");
      const perRecipe = await Promise.all(recipes.map(async (recipe) => {
        const versions = await json<readonly { id: string; versionNumber: number; status: string }[]>(`/recipes/${recipe.id}/versions`);
        return versions.filter((version) => version.status === "Active").map((version) => ({ id: version.id, label: `${recipe.name} · sürüm ${version.versionNumber}` }));
      }));
      return perRecipe.flat();
    },
    listLocations: () => json("/inventory/stock-locations?activeOnly=true"),
    listStockItems: () => json("/inventory/stock-items?activeOnly=true"),
  };
}
