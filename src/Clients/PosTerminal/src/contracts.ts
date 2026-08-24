export interface LoginResponse {
  userId: string;
  displayName: string;
  terminalId: string;
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
