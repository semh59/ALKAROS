import type { CostSnapshot, DailyMenu, DailyMenuDetails, Menu, MenuComposition, ProductOption, RecipeVersionOption } from "./models";

export class MenuApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) { super(message); }
}

const fallbackByStatus = (status: number): string =>
  status === 401 ? "Oturum sona erdi; yeniden giriş yapın."
    : status === 403 ? "Bu işlem için yetkiniz yok."
    : status === 409 ? "Kayıt başka biri tarafından değiştirildi; yenileyip tekrar deneyin."
    : "İşlem tamamlanamadı.";

function createRequester(fetcher: typeof fetch) {
  return async function call(path: string, init: RequestInit = {}): Promise<Response> {
    let response: Response;
    try {
      response = await fetcher(`/api/v1/management${path}`, {
        ...init,
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID() },
        signal: AbortSignal.timeout(8_000),
      });
    } catch {
      throw new MenuApiError(0, "NETWORK_UNAVAILABLE", "Sunucuya ulaşılamadı. Bağlantıyı kontrol edip tekrar deneyin.");
    }
    if (response.ok) return response;
    const body = await response.json().catch(() => undefined) as { error?: { code?: string; message?: string } } | undefined;
    throw new MenuApiError(response.status, body?.error?.code ?? "REQUEST_FAILED", body?.error?.message ?? fallbackByStatus(response.status));
  };
}

export interface MenusClient {
  listMenus: () => Promise<readonly Menu[]>;
  getMenu: (menuId: string) => Promise<MenuComposition>;
  createMenu: (code: string, name: string) => Promise<void>;
  setMenuActive: (menu: Menu, isActive: boolean) => Promise<void>;
  addMenuItem: (menuId: string, productId: string, displayOrder: number) => Promise<void>;
  setMenuItem: (menuId: string, menuItemId: string, displayOrder: number, isActive: boolean) => Promise<void>;
  getDailyMenu: (date: string) => Promise<DailyMenuDetails | null>;
  createDailyMenu: (date: string, note: string) => Promise<DailyMenu>;
  openDailyMenu: (dailyMenuId: string) => Promise<void>;
  closeDailyMenu: (dailyMenuId: string) => Promise<void>;
  addDailyMenuItem: (dailyMenuId: string, input: { productId: string; price: number | null; plannedPortions: number; recipeVersionId: string | null }) => Promise<void>;
  setDailyItemPrice: (itemId: string, price: number) => Promise<void>;
  setDailyItemPortions: (itemId: string, portions: number) => Promise<void>;
  setDailyItemActive: (itemId: string, isActive: boolean) => Promise<void>;
  listProducts: () => Promise<readonly ProductOption[]>;
  listRecipeVersions: () => Promise<readonly RecipeVersionOption[]>;
}

export function createMenusClient(fetcher: typeof fetch = fetch): MenusClient {
  const call = createRequester(fetcher);
  const json = async <T>(path: string): Promise<T> => (await call(path)).json() as Promise<T>;
  const send = (method: string, path: string, body: unknown = {}) => call(path, { method, body: JSON.stringify(body) }).then(() => undefined);
  const base = "/menus-and-specials";
  return {
    listMenus: () => json(`${base}/menus`),
    getMenu: (menuId) => json(`${base}/menus/${menuId}`),
    createMenu: (code, name) => send("POST", `${base}/menus`, { code, name }),
    setMenuActive: (menu, isActive) => send("PUT", `${base}/menus/${menu.id}`, { name: menu.name, isActive }),
    addMenuItem: (menuId, productId, displayOrder) => send("POST", `${base}/menus/${menuId}/items`, { productId, displayOrder }),
    setMenuItem: (menuId, menuItemId, displayOrder, isActive) => send("PUT", `${base}/menus/${menuId}/items/${menuItemId}`, { displayOrder, isActive }),
    async getDailyMenu(date) {
      try {
        return await json<DailyMenuDetails>(`${base}/daily-menus/by-date/${date}`);
      } catch (reason) {
        if (reason instanceof MenuApiError && reason.status === 404) return null;
        throw reason;
      }
    },
    createDailyMenu: async (date, note) => (await call(`${base}/daily-menus`, { method: "POST", body: JSON.stringify({ businessDate: date, note: note.trim() || null }) })).json() as Promise<DailyMenu>,
    openDailyMenu: (dailyMenuId) => send("POST", `${base}/daily-menus/${dailyMenuId}/open`, { openedAt: null }),
    closeDailyMenu: (dailyMenuId) => send("POST", `${base}/daily-menus/${dailyMenuId}/close`, { closedAt: null }),
    addDailyMenuItem: (dailyMenuId, input) => send("POST", `${base}/daily-menus/${dailyMenuId}/items`, { ...input, printerRoutePolicy: null }),
    setDailyItemPrice: (itemId, price) => send("PUT", `${base}/daily-menus/items/${itemId}/price`, { newPrice: price }),
    setDailyItemPortions: (itemId, portions) => send("PUT", `${base}/daily-menus/items/${itemId}/planned-portions`, { newPlannedPortions: portions }),
    setDailyItemActive: (itemId, isActive) => send("PUT", `${base}/daily-menus/items/${itemId}/status`, { isActive }),
    async listProducts() {
      const result = await json<{ items: ProductOption[] } | ProductOption[]>("/catalog/products?limit=100");
      return Array.isArray(result) ? result : result.items;
    },
    listRecipeVersions: () => listRecipeVersions(json),
  };
}

async function listRecipeVersions(json: <T>(path: string) => Promise<T>): Promise<readonly RecipeVersionOption[]> {
  const recipes = await json<readonly { id: string; name: string }[]>("/recipes");
  const perRecipe = await Promise.all(recipes.map(async (recipe) => {
    const versions = await json<readonly { id: string; versionNumber: number; status: string }[]>(`/recipes/${recipe.id}/versions`);
    return versions.map((version) => ({ id: version.id, label: `${recipe.name} · sürüm ${version.versionNumber}`, status: version.status }));
  }));
  return perRecipe.flat();
}

export interface CostClient {
  listRecipeVersions: () => Promise<readonly RecipeVersionOption[]>;
  getEffective: (versionId: string, asOf: string) => Promise<CostSnapshot | null>;
  calculate: (versionId: string, basisDate: string) => Promise<CostSnapshot>;
  listStockItemNames: () => Promise<ReadonlyMap<string, string>>;
}

export function createCostClient(fetcher: typeof fetch = fetch): CostClient {
  const call = createRequester(fetcher);
  const json = async <T>(path: string): Promise<T> => (await call(path)).json() as Promise<T>;
  const stockItems = () => json<readonly { id: string; name: string; trackingUnitCode: string }[]>("/inventory/stock-items");
  return {
    listRecipeVersions: () => listRecipeVersions(json),
    async getEffective(versionId, asOf) {
      try {
        return await json<CostSnapshot>(`/recipes/${versionId}/cost-snapshots/effective?asOfDate=${asOf}`);
      } catch (reason) {
        if (reason instanceof MenuApiError && reason.status === 404) return null;
        throw reason;
      }
    },
    async calculate(versionId, basisDate) {
      const items = await stockItems();
      const stockItemUnits = Object.fromEntries(items.map((item) => [item.id, item.trackingUnitCode]));
      return (await call(`/recipes/${versionId}/cost-snapshots`, {
        method: "POST", body: JSON.stringify({ costBasisDate: basisDate, stockItemUnits }),
      })).json() as Promise<CostSnapshot>;
    },
    async listStockItemNames() {
      return new Map((await stockItems()).map((item) => [item.id, item.name]));
    },
  };
}
