export interface OrderInvoiceRow {
  invoiceId: string; orderId: string; orderNumber: string; provider: string; issueDate: string; serviceDate: string;
  status: string; netAmount: number; taxAmount: number; grossAmount: number;
}

export interface MissingOrderInvoice {
  orderId: string; orderNumber: string; provider: string; total: number; deliveredAt: string; daysLeft: number;
}

export interface OrderInvoiceList {
  from: string; to: string; invoices: readonly OrderInvoiceRow[]; missing: readonly MissingOrderInvoice[];
}

export interface OrderInvoiceDetail {
  orderNumber: string; provider: string; webAddress: string; serviceDate: string;
  seller: { legalName: string; taxIdNumber: string; taxOffice: string; address: string };
  lines: readonly { lineNumber: number; description: string; quantity: number; taxRate: number; grossAmount: number }[];
}

const statuses: Record<string, string> = { Draft: "Taslak" };

export const statusLabel = (status: string) => statuses[status] ?? "Bilinmiyor";
