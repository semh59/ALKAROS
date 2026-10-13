import { ManagementApiError, createRequester } from "../management/http";
import type { ProductMapping, ProductOption, Recipe, RecipeVersion, StockItemOption, UnitConversion } from "./models";

export interface IngredientInput { ingredientItemId: string; quantity: number; unitCode: string; lossPercentage: number }

export interface RecipesClient {
  listRecipes: () => Promise<readonly Recipe[]>;
  createRecipe: (code: string, name: string, description: string) => Promise<void>;
  listVersions: (recipeId: string) => Promise<readonly RecipeVersion[]>;
  createFirstVersion: (recipeId: string, yieldQuantity: number, yieldUnitCode: string) => Promise<void>;
  createNextVersion: (recipeId: string) => Promise<void>;
  addIngredient: (versionId: string, input: IngredientInput) => Promise<void>;
  removeIngredient: (versionId: string, ingredientItemId: string) => Promise<void>;
  activate: (recipeId: string, versionNumber: number) => Promise<void>;
  lock: (versionId: string) => Promise<void>;
  listConversions: () => Promise<readonly UnitConversion[]>;
  createConversion: (fromUnitCode: string, toUnitCode: string, factor: number) => Promise<void>;
  listStockItems: () => Promise<readonly StockItemOption[]>;
  listProducts: () => Promise<readonly ProductOption[]>;
  getMapping: (productId: string) => Promise<ProductMapping | null>;
  assignMapping: (productId: string, recipeId: string) => Promise<void>;
  removeMapping: (productId: string) => Promise<void>;
}

export function createRecipesClient(fetcher: typeof fetch = fetch): RecipesClient {
  const call = createRequester(fetcher, { idempotentPosts: true });
  const json = async <T>(path: string): Promise<T> => (await call(path)).json() as Promise<T>;
  const send = (method: string, path: string, body?: unknown) =>
    call(path, { method, ...(body === undefined ? {} : { body: JSON.stringify(body) }) }).then(() => undefined);
  return {
    listRecipes: () => json("/recipes"),
    createRecipe: (code, name, description) => send("POST", "/recipes", { code, name, description: description.trim() || null }),
    listVersions: (recipeId) => json(`/recipes/${recipeId}/versions`),
    createFirstVersion: (recipeId, yieldQuantity, yieldUnitCode) => send("POST", `/recipes/${recipeId}/versions`, { yieldQuantity, yieldUnitCode }),
    createNextVersion: (recipeId) => send("POST", `/recipes/${recipeId}/versions/next`),
    addIngredient: (versionId, input) => send("POST", `/recipes/versions/${versionId}/ingredients`, input),
    removeIngredient: (versionId, ingredientItemId) => send("DELETE", `/recipes/versions/${versionId}/ingredients/${ingredientItemId}`),
    activate: (recipeId, versionNumber) => send("POST", `/recipes/${recipeId}/versions/${versionNumber}/activate`),
    lock: (versionId) => send("POST", `/recipes/versions/${versionId}/lock`),
    listConversions: () => json("/recipes/unit-conversions"),
    createConversion: (fromUnitCode, toUnitCode, factor) => send("POST", "/recipes/unit-conversions", { fromUnitCode, toUnitCode, factor }),
    listStockItems: () => json("/inventory/stock-items"),
    async listProducts() {
      const result = await json<{ items: ProductOption[] } | ProductOption[]>("/catalog/products?limit=100");
      return Array.isArray(result) ? result : result.items;
    },
    async getMapping(productId) {
      try {
        return await json<ProductMapping>(`/recipes/products/${productId}/mapping`);
      } catch (reason) {
        if (reason instanceof ManagementApiError && reason.status === 404) return null;
        throw reason;
      }
    },
    assignMapping: (productId, recipeId) => send("POST", `/recipes/products/${productId}/mapping`, { recipeId, isActive: true, notes: null }),
    removeMapping: (productId) => send("DELETE", `/recipes/products/${productId}/mapping`),
  };
}
