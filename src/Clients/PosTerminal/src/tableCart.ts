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

/** Reads the mark without clearing it, so a reloaded sales screen shows the same table cart again. */
export function readTableCart(): string | null {
  try {
    return sessionStorage.getItem(key);
  } catch { return null; }
}

/** The cashier left the table cart (quick sale, another screen, sign-out): the next visit to the sales screen starts clean. */
export function forgetTableCart(): void {
  try {
    sessionStorage.removeItem(key);
    sessionStorage.removeItem(withoutKitchenKey);
  } catch { /* storage unavailable: nothing was kept */ }
}
