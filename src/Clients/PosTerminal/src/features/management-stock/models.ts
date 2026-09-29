export interface StockItem {
  id: string;
  code: string;
  name: string;
  itemType: string;
  trackingUnitCode: string;
  defaultLocationId: string | null;
  isActive: boolean;
  reorderPoint: number | null;
}

export interface StockLocation {
  id: string;
  code: string;
  name: string;
  locationType: string;
  isActive: boolean;
}

export interface CriticalStockItem {
  stockItemId: string;
  stockItemName: string;
  trackingUnitCode: string;
  locationName: string;
  onHandQuantity: number;
  availableQuantity: number;
  criticalThreshold: number;
  isCritical: boolean;
}

export interface CriticalStockReport {
  items: readonly CriticalStockItem[];
  totalCriticalItemsCount: number;
}

export interface VarianceItem {
  stockItemId: string;
  stockItemName: string;
  trackingUnitCode: string;
  locationName: string;
  actualUsage: number;
  theoreticalUsage: number;
  varianceQuantity: number;
  variancePercentage: number | null;
}

export interface VarianceReport {
  items: readonly VarianceItem[];
  excludedForMissingCountsCount: number;
}

export const itemTypes = ["RawMaterial", "Portion", "Packaging", "ServiceItem"] as const;
export const locationTypes = ["Warehouse", "Kitchen", "Bar", "ColdStorage", "DryStorage", "Counter"] as const;
export const wasteSources = ["Manual", "Spoilage", "Expiration", "PreparationDamage"] as const;

const label = (map: Record<string, string>) => (value: string) => map[value] ?? "Bilinmiyor";

export const itemTypeLabel = label({ RawMaterial: "Hammadde", Portion: "Porsiyon", Packaging: "Ambalaj", ServiceItem: "Hizmet kalemi" });
export const locationTypeLabel = label({
  Warehouse: "Depo", Kitchen: "Mutfak", Bar: "Bar", ColdStorage: "Soğuk hava deposu", DryStorage: "Kuru depo", Counter: "Kasa reyonu",
});
export const wasteSourceLabel = label({ Manual: "Elle kayıt", Spoilage: "Bozulma", Expiration: "Son kullanma tarihi", PreparationDamage: "Hazırlık hasarı" });

/** The quantity fields accept a decimal comma; anything that is not a finite number is rejected. */
export function parseQuantity(text: string): number | null {
  const value = Number(text.trim().replace(",", "."));
  return text.trim() !== "" && Number.isFinite(value) ? value : null;
}
