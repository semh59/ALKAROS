export interface Supplier { id: string; code: string; name: string; active: boolean }
export interface NewSupplier { code: string; name: string; taxNumber: string | null; taxOffice: string | null; phone: string | null; email: string | null }

export interface PurchaseOrderLine {
  id: string; stockItemId: string; orderedQuantity: number; receivedQuantity: number; openQuantity: number;
  unitCode: string; unitPrice: number; totalPrice: number; status: string;
}
export interface PurchaseOrder {
  id: string; orderNumber: string; supplierId: string; status: string; destinationLocationId: string;
  totalAmount: number; currency: string; lines: readonly PurchaseOrderLine[];
}
export interface NewOrderLine { stockItemId: string; orderedQuantity: number; unitCode: string; unitPrice: number }
export interface ReceiptLine { orderLineId: string; deliveredQuantity: number; varianceReason: string | null }

export interface Batch {
  id: string; batchNumber: string; recipeVersionId: string; status: string; plannedQuantity: number; actualQuantity: number;
  portionUnitCode: string; cancellationReason: string | null;
}
export interface BatchCompletion { actualQuantity: number; sourceLocationId: string; destinationLocationId: string | null; outputStockItemId: string | null }

export interface LookupLocation { id: string; name: string }
export interface LookupItem { id: string; name: string; trackingUnitCode: string }
export interface RecipeVersionOption { id: string; label: string }

export const orderStatuses = ["Draft", "Submitted", "PartiallyReceived", "Completed", "Cancelled"] as const;
export const batchStatuses = ["Planned", "InProgress", "Completed", "Cancelled"] as const;

const label = (map: Record<string, string>) => (value: string) => map[value] ?? "Bilinmiyor";
export const orderStatusLabel = label({ Draft: "Taslak", Submitted: "Gönderildi", PartiallyReceived: "Kısmen teslim alındı", Completed: "Tamamlandı", Cancelled: "İptal edildi" });
export const lineStatusLabel = label({ Pending: "Bekliyor", PartiallyReceived: "Kısmen alındı", Completed: "Tamamlandı" });
export const batchStatusLabel = label({ Planned: "Planlandı", InProgress: "Üretimde", Completed: "Tamamlandı", Cancelled: "İptal edildi" });

export function parseQuantity(text: string): number | null {
  const value = Number(text.trim().replace(",", "."));
  return text.trim() !== "" && Number.isFinite(value) ? value : null;
}
