export type { LookupItem, LookupLocation } from "../management-purchasing-production/models";
export { parseQuantity } from "../management-purchasing-production/models";

export interface PurchaseInvoiceSummary {
  invoiceId: string; invoiceNumber: string; issueDate: string; supplierName: string; supplierId: string | null;
  status: string; lineCount: number; unmappedLineCount: number; netTotal: number;
}
export interface PurchaseInvoiceLine {
  lineId: string; lineNumber: number; description: string; quantity: number; unitCode: string; unitPrice: number; lineNet: number;
  stockItemId: string | null; conversionFactor: number | null;
}
export interface PurchaseInvoice {
  invoiceId: string; invoiceNumber: string; issueDate: string; supplierName: string; supplierId: string | null; status: string;
  currency: string; lines: readonly PurchaseInvoiceLine[];
}

export const invoiceStatuses = ["Draft", "Approved", "Rejected"] as const;
const statusLabels: Record<string, string> = { Draft: "Taslak", Approved: "Onaylandı", Rejected: "Reddedildi" };
export const invoiceStatusLabel = (status: string) => statusLabels[status] ?? "Bilinmiyor";
