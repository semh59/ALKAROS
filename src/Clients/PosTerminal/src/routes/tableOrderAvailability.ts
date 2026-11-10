interface OrderableTable {
  active: boolean;
  status: string;
}

/** A table takes an order when it is free or when guests were seated (with or without an order to continue). */
export const canStartTableOrder = (table: OrderableTable | null): boolean =>
  table?.active === true && (table.status === "Available" || table.status === "Occupied");
