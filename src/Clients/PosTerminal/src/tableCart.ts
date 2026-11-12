const key = "alkaros.table-cart-order";

/** The cashier opened this table order from the tables screen on purpose: the sales screen may show it as the cart. */
export function markTableCart(orderId: string): void {
  try { sessionStorage.setItem(key, orderId); } catch { /* storage unavailable: the order is then not shown as the cart */ }
}

/** Reads and clears the mark; the sales screen asks once each time it is entered. */
export function takeTableCart(): string | null {
  try {
    const orderId = sessionStorage.getItem(key);
    sessionStorage.removeItem(key);
    return orderId;
  } catch { return null; }
}
