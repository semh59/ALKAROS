// V14-NFC-001/004: the NFC self-service order surface has no session of any
// kind, so its result mirrors the server's OrderDto directly — the client
// never re-derives status/total, only displays what the API returned.
export interface NfcOrderResultItem {
  itemId: string;
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  specialInstructions: string | null;
}

export interface NfcOrderResult {
  orderId: string;
  tableId: string;
  tableNumber: string;
  status: string;
  rowVersion: number;
  totalAmount: number;
  items: NfcOrderResultItem[];
  createdAt: string;
}

export interface LoginResponse {
  userId: string;
  displayName: string;
  terminalId: string;
  capabilities?: string[];
}

export interface RuntimeConfiguration {
  kitchenStationId: string;
  // Absolute origin the customer display is served from (finding B-4). Absent
  // in single-origin / legacy deployments; the display link then stays relative.
  customerDisplayUrl?: string;
  // V1-SET-003: whether this deployment has a dedicated Reservation Station
  // screen (/reservations). Off by default — reservation intake then stays
  // on the cashier's own floor-plan screen, unchanged.
  reservationStationEnabled: boolean;
}

export interface CatalogProduct {
  productId: string;
  sku: string;
  name: string;
  categoryCode: string;
  categoryName: string;
  unitPrice: number;
  taxRate: number;
}

export interface DisplayLine {
  itemId: string;
  name: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface DisplaySnapshot {
  displayId: string;
  terminalId: string;
  orderId: string | null;
  revision: number;
  state: "Idle" | "Active" | "Paying" | "Completed" | "Unavailable";
  editable: boolean;
  orderNumber: string | null;
  lines: DisplayLine[];
  subtotal: number;
  discountTotal: number;
  taxTotal: number;
  total: number;
  currency: string;
  serverTimestamp: string;
  message: string;
}

export interface PairingCreated {
  requestId: string;
  displayId: string;
  secret: string;
  code: string;
  expiresAt: string;
}

export interface PairingCompleted {
  displayId: string;
  terminalId: string;
  expiresAt: string;
}

export interface MutationResult {
  orderId: string;
  revision: number;
}

export interface ApiErrorBody {
  error?: {
    code?: string;
    message?: string;
  };
}
