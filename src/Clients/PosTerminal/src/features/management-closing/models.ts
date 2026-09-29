export interface BusinessDay {
  businessDayId: string;
  businessDate: string;
  openedAt: string;
  closedAt: string | null;
  status: string;
  totalRevenue: number;
  totalOrdersCount: number;
  totalCancelledItemsCount: number;
  totalPrintFailuresCount: number;
}

export interface WaiterSummary {
  waiterUserId: string;
  ordersServedCount: number;
  totalSalesAmount: number;
  cancellationsCount: number;
  discountsAppliedAmount: number;
}

export interface BusinessDayReport {
  businessDay: BusinessDay;
  waiterSummaries: readonly WaiterSummary[];
}

export interface SettlementReport {
  paymentMix: readonly { method: string; approvedCount: number; approvedAmount: number }[];
  unsettledPayments: { unknownCount: number; unknownAmount: number; reconciliationRequiredCount: number; reconciliationRequiredAmount: number };
  cashSessions: readonly { cashSessionId: string; status: string; expectedCash: number; actualCash: number; difference: number; isOpen: boolean }[];
  reconciliationTotals: readonly { caseType: string; openCount: number; resolvedCount: number; openDiscrepancyAmount: number }[];
}

export interface ManualConfirmation {
  confirmationId: string;
  slipNumber: string;
  amount: number;
  status: string;
  requestedAt: string;
  decidedAt: string | null;
}

export interface ReconciliationCase {
  caseId: string;
  caseType: string;
  severity: string;
  status: string;
  discrepancyAmount: number;
  openedAt: string;
  rowVersion: number;
}

export const caseStatuses = ["Open", "Investigating", "Escalated", "Resolved", "Dismissed"] as const;
export type CaseStatus = (typeof caseStatuses)[number];

const unknownLabel = "Bilinmiyor";
const label = (map: Record<string, string>) => (value: string) => map[value] ?? unknownLabel;

export const dayStatusLabel = label({ Open: "Açık", Closed: "Kapalı" });
export const caseStatusLabel = label({ Open: "Açık", Investigating: "İnceleniyor", Escalated: "Üst makama iletildi", Resolved: "Çözüldü", Dismissed: "Reddedildi" });
export const caseTypeLabel = label({
  PaymentMismatch: "Ödeme uyuşmazlığı",
  CashVariance: "Kasa farkı",
  FiscalDiscrepancy: "Mali belge farkı",
  OnlineOrderMismatch: "Online sipariş uyuşmazlığı",
  InventoryDiscrepancy: "Stok farkı",
});
export const severityLabel = label({ Low: "Düşük", Medium: "Orta", High: "Yüksek", Critical: "Kritik" });
export const tenderLabel = label({
  Cash: "Nakit",
  BankCard: "Banka kartı",
  MealCard: "Yemek kartı",
  Eft: "EFT / Havale",
  CustomerAccount: "Cari hesap",
});
export const confirmationStatusLabel = label({ Pending: "Bekliyor", Approved: "Onaylandı", Rejected: "Reddedildi" });

/** A case moves only along the server's own transition table; `Resolved` for an online order case has its own screen. */
export function nextStatuses(status: string, caseType: string): readonly CaseStatus[] {
  const targets: Record<string, readonly CaseStatus[]> = {
    Open: ["Investigating", "Escalated", "Resolved", "Dismissed"],
    Investigating: ["Escalated", "Resolved", "Dismissed"],
    Escalated: ["Investigating", "Resolved", "Dismissed"],
  };
  return (targets[status] ?? []).filter((target) => !(target === "Resolved" && caseType === "OnlineOrderMismatch"));
}

export const transitionActionLabel = label({
  Investigating: "İncelemeye al",
  Escalated: "Üst makama ilet",
  Resolved: "Çözüldü olarak kapat",
  Dismissed: "Reddet",
});
