const key = "alkaros.table-cart-order";
const withoutKitchenKey = "alkaros.table-cart-without-kitchen";

/** The cashier opened this table order from the tables screen on purpose: the sales screen may show it as the cart. */
export function markTableCart(orderId: string, withoutKitchen = false): void {
  try {
    sessionStorage.setItem(key, orderId);
    if (withoutKitchen) sessionStorage.setItem(withoutKitchenKey, "1");
    else sessionStorage.removeItem(withoutKitchenKey);
  } catch { /* storage unavailable: the order is then not shown as the cart */ }
}

/** True once when the cashier chose to add to the table without a kitchen ticket: the sales screen then opens the reason picker. */
export function takeWithoutKitchenIntent(): boolean {
  try {
    const wanted = sessionStorage.getItem(withoutKitchenKey) !== null;
    sessionStorage.removeItem(withoutKitchenKey);
    return wanted;
  } catch { return false; }
}

/** Reads and clears the mark; the sales screen asks once each time it is entered. */
export function takeTableCart(): string | null {
  try {
    const orderId = sessionStorage.getItem(key);
    sessionStorage.removeItem(key);
    return orderId;
  } catch { return null; }
}
