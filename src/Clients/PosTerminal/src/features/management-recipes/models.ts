export interface Recipe { id: string; code: string; name: string; description: string | null }
export interface RecipeIngredient { ingredientItemId: string; quantity: number; unitCode: string; lossPercentage: number; sortOrder: number; notes: string | null }
export interface RecipeVersion {
  id: string; recipeId: string; versionNumber: number; status: string; yieldQuantity: number; yieldUnitCode: string;
  preparationMinutes: number; isLocked: boolean; ingredients: readonly RecipeIngredient[];
}
export interface UnitConversion { id: string; fromUnitCode: string; toUnitCode: string; factor: number }
export interface StockItemOption { id: string; name: string; trackingUnitCode: string }
export interface ProductOption { id: string; name: string }
export interface ProductMapping { productId: string; recipeId: string; isActive: boolean; notes: string | null }

const label = (map: Record<string, string>) => (value: string) => map[value] ?? "Bilinmiyor";
export const versionStatusLabel = label({ Draft: "Taslak", Active: "Etkin", Archived: "Arşivlendi", Deprecated: "Kullanımdan kaldırıldı" });

export function parseNumber(text: string): number | null {
  const value = Number(text.trim().replace(",", "."));
  return text.trim() !== "" && Number.isFinite(value) ? value : null;
}
