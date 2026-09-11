export type CatalogWorkspaceState =
  | "loading"
  | "ready"
  | "empty"
  | "busy"
  | "error"
  | "offline"
  | "stale"
  | "unauthorized"
  | "conflict";

export type CatalogEntityKind = "categories" | "taxes" | "products" | "modifierGroups" | "modifiers" | "prices";

export interface CatalogCategory {
  id: string;
  code: string;
  name: string;
  parentId: string | null;
  sortOrder: number;
  active: boolean;
}

export interface CatalogTaxProfile {
  id: string;
  code: string;
  name: string;
  vatRate: number;
  active: boolean;
}

export interface CatalogProduct {
  id: string;
  sku: string;
  name: string;
  productType: string;
  stockMode: string;
  categoryId: string | null;
  taxProfileId: string | null;
  description: string | null;
  printerRoutePolicy: string | null;
  displayOrder: number;
  currentPrice: number | null;
  active: boolean;
  isAvailable: boolean;
  // V1-WTR-017: manager-entered estimated kitchen prep time in minutes
  // (1-180), null when never set. Lets the Garson client warn before
  // sending a round whose items' prep times are far apart.
  prepTimeMinutes: number | null;
}

export interface CatalogModifierGroup {
  id: string;
  code: string;
  name: string;
  selectionType: string;
  minSelections: number;
  maxSelections: number;
  active: boolean;
}

export interface CatalogModifier {
  id: string;
  modifierGroupId: string;
  code: string;
  name: string;
  priceDelta: number;
  productId: string | null;
  active: boolean;
}

export interface CatalogPrice {
  id: string;
  productId: string;
  priceType: string;
  price: number;
  currencyCode: string;
  effectiveFrom: string;
  effectiveTo: string | null;
}

export interface CatalogData {
  categories: readonly CatalogCategory[];
  taxes: readonly CatalogTaxProfile[];
  products: readonly CatalogProduct[];
  modifierGroups: readonly CatalogModifierGroup[];
  modifiers: readonly CatalogModifier[];
  prices: readonly CatalogPrice[];
}

export type CatalogCreateInput =
  | { kind: "categories"; value: Omit<CatalogCategory, "active" | "parentId"> & { parentId?: string | null; active?: boolean } }
  | { kind: "taxes"; value: Omit<CatalogTaxProfile, "active"> & { active?: boolean } }
  | { kind: "products"; value: Omit<CatalogProduct, "active" | "isAvailable" | "prepTimeMinutes"> & { active?: boolean; isAvailable?: boolean; prepTimeMinutes?: number | null } }
  | { kind: "modifierGroups"; value: Omit<CatalogModifierGroup, "active"> & { active?: boolean } }
  | { kind: "modifiers"; value: Omit<CatalogModifier, "active"> & { active?: boolean } }
  | { kind: "prices"; value: Omit<CatalogPrice, "effectiveTo"> & { effectiveTo?: string | null } };

export interface CatalogWorkspaceProps {
  state: CatalogWorkspaceState;
  data: CatalogData;
  canManage: boolean;
  onRefresh: () => void | Promise<void>;
  onCreate?: (input: CatalogCreateInput) => void | Promise<void>;
  onSetAvailability?: (productId: string, isAvailable: boolean) => void | Promise<void>;
  onSetPrepTime?: (productId: string, prepTimeMinutes: number | null) => void | Promise<void>;
  errorMessage?: string;
  lastUpdated?: string;
}

// Enum label maps live in the central catalog (finding F-7).
export { catalogAddLabels, catalogEntityLabels, productTypeLabels, selectionTypeLabels } from "../../strings";
