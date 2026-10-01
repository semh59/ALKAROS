export interface ProductMarginRow {
  productId: string; productName: string; soldQuantity: number; givenAwayQuantity: number;
  netRevenue: number; cost: number; unknownCostLines: number; grossMargin: number | null; marginPercent: number | null;
}
export interface ProductMarginReport {
  reportVersion: string; from: string; to: string; rows: readonly ProductMarginRow[];
  totalNetRevenue: number; totalCost: number; unknownCostLines: number;
  check: { productNetTotal: number; linesNetTotal: number; billLevelDiscounts: number; isBalanced: boolean };
}

export const formatQuantity = (value: number) => value.toLocaleString("tr-TR", { maximumFractionDigits: 3 });
export const formatPercent = (value: number) => `%${value.toLocaleString("tr-TR", { maximumFractionDigits: 2 })}`;
