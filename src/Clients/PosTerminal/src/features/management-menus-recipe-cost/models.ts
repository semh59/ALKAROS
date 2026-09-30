export interface Menu { id: string; code: string; name: string; isActive: boolean }
export interface MenuItem { menuItemId: string; productId: string; productName: string; isProductActiveInCatalog: boolean; displayOrder: number; isActive: boolean }
export interface MenuComposition { menu: Menu; items: readonly MenuItem[] }

export interface DailyMenu { id: string; businessDate: string; status: string; note: string | null }
export interface DailyMenuItem {
  id: string; productNameSnapshot: string; price: number; plannedPortions: number; preparedPortions: number;
  availablePortions: number; isOutOfStock: boolean; isActive: boolean;
}
export interface DailyMenuDetails { menu: DailyMenu; items: readonly DailyMenuItem[] }

export interface ProductOption { id: string; name: string }
export interface RecipeVersionOption { id: string; label: string; status: string }

export interface CostSnapshotItem { stockItemId: string; effectiveNativeQuantity: number; nativeUnitCode: string; unitCost: number; lineCost: number }
export interface CostSnapshot {
  id: string; costBasisDate: string; calculatedCost: number; costPerPortion: number; currency: string; items: readonly CostSnapshotItem[];
}

const label = (map: Record<string, string>) => (value: string) => map[value] ?? "Bilinmiyor";
export const dailyMenuStatusLabel = label({ Draft: "Taslak", Open: "Açık", PartiallyConsumed: "Kısmen tüketildi", Closed: "Kapalı" });
export const recipeStatusLabel = label({ Draft: "Taslak", Active: "Etkin", Archived: "Arşivlendi", Deprecated: "Kullanımdan kaldırıldı" });

export function parseNumber(text: string): number | null {
  const value = Number(text.trim().replace(",", "."));
  return text.trim() !== "" && Number.isFinite(value) ? value : null;
}
