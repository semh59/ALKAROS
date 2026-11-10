interface OrderableTable {
  active: boolean;
  status: string;
  currentOrderId: string | null;
}

/** A table takes an order when it is free, when guests were seated without an order yet, or when its order can be continued. */
export const canStartTableOrder = (table: OrderableTable | null): boolean =>
  table?.active === true && (table.status === "Available" || table.status === "Occupied");
