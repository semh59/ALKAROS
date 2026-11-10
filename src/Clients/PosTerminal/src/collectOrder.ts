import { createBillFromOrder } from "./features/billing";
import { collectionHref } from "./features/pending-checks/pendingChecksApi";

/** The payment page for an order: the bill it already has, or the one opened from it now. */
export async function collectionHrefForOrder(terminalId: string, orderId: string, billId: string | null): Promise<string> {
  return collectionHref(billId ?? (await createBillFromOrder(terminalId, orderId)).billId);
}
