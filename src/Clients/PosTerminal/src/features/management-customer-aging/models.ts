export interface CustomerAgingRow {
  customerId: string; name: string;
  days0To30: number; days31To60: number; days61To90: number; over90: number;
  totalDebt: number; unappliedCredit: number;
  balance30DaysAgo: number | null; balance60DaysAgo: number | null; balance90DaysAgo: number | null;
}

export interface CustomerAgingReport {
  asOf: string; rows: readonly CustomerAgingRow[]; check: { isBalanced: boolean };
}

export interface CustomerAgingTotals {
  days0To30: number; days31To60: number; days61To90: number; over90: number; totalDebt: number; unappliedCredit: number;
}

export function totalsOf(rows: readonly CustomerAgingRow[]): CustomerAgingTotals {
  return rows.reduce<CustomerAgingTotals>((sum, row) => ({
    days0To30: sum.days0To30 + row.days0To30,
    days31To60: sum.days31To60 + row.days31To60,
    days61To90: sum.days61To90 + row.days61To90,
    over90: sum.over90 + row.over90,
    totalDebt: sum.totalDebt + row.totalDebt,
    unappliedCredit: sum.unappliedCredit + row.unappliedCredit,
  }), { days0To30: 0, days31To60: 0, days61To90: 0, over90: 0, totalDebt: 0, unappliedCredit: 0 });
}

export const formatDay = (iso: string) => new Date(`${iso}T00:00:00`).toLocaleDateString("tr-TR");
