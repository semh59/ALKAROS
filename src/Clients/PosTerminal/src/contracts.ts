// V12-NFC-001/004: the NFC self-service order surface has no session of any
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

// V12-QRT-003 (configured/updatedAt) + V12-QRT-001 (accountId/zoneId/
// baseDomain, tunnelHostname/tunnelUpdatedAt, connectorState — none of these
// are secret, so they round-trip back; the token and tunnel run-token never do).
export interface RelayCredentialStatus {
  configured: boolean;
  updatedAt: string | null;
  accountId: string | null;
  zoneId: string | null;
  baseDomain: string | null;
  tunnelHostname: string | null;
  tunnelUpdatedAt: string | null;
  // Raw server enum name (ALKAROS.QrRelay.LocalConnector.RelayConnectorState)
  // — the UI never shows this directly, see RelaySettings.tsx's Turkish
  // mapping. Narrowed to the real 4-member union (not a bare `string`) so
  // RelaySettings.tsx's label map is TypeScript-exhaustive: a 5th backend
  // value added without updating that map is a compile error here, not a
  // silent raw-enum leak to the screen.
  //
  // V1-RMD-324 (independent 2026-09-26 audit, finding K17): "Unknown" - the
  // status row has not been refreshed recently enough to trust (the
  // connector's own container most likely died) - is the 4th, added here.
  connectorState: "NotConfigured" | "Running" | "Restarting" | "Unknown";
}

// V1-RMD-331 (independent 2026-09-26 audit, finding K10): resolves a
// manager-supplied username into the userId SecurityAdministration's
// revoke-sessions/force-unlock actions require. isLocked reflects the
// account's password lockout only (matches AccountRecoveryService.ForceUnlockAsync's
// own scope, never the separate PIN lockout).
export interface UserLookupResult {
  userId: string;
  displayName: string;
  active: boolean;
  isLocked: boolean;
}

// V14-QNB-006: userId/vergiTcKimlikNo are not secret (they identify WHICH
// tenant, not a credential) so they round-trip back; the password never does.
export interface QnbCredentialStatus {
  configured: boolean;
  updatedAt: string | null;
  userId: string | null;
  vergiTcKimlikNo: string | null;
}

// V14-QNB-007: the real result of attempting a `wsLogin` against QNB's own
// live test server with the saved credential — `message` is always a
// pre-written Turkish sentence (docs/UI_STYLE_GUIDE.md §3), never QNB's raw
// SOAP fault text.
export interface QnbConnectionTestResult {
  success: boolean;
  message: string;
}

// V13-HUG-005: merchantId/branchId/terminalId/clientId are not secret (they
// identify WHICH terminal, not a credential) so they round-trip back; the
// client secret never does.
export interface TokenTerminalCredentialStatus {
  configured: boolean;
  updatedAt: string | null;
  merchantId: string | null;
  branchId: string | null;
  terminalId: string | null;
  clientId: string | null;
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

// V1-CUI-012: the business's own name/color/logo (V1-SET-007/008), read
// through the same public, session-free endpoint the QR customer pages use.
export interface QrBrandingResponse {
  businessName: string;
  accentColor: string;
  hasLogo: boolean;
}

// V1-CUI-012: one selectable accent color — never a free hex field (the
// backend owns the design decision, docs/design/foundations.md §0). The
// settings screen renders exactly this list, it never invents its own.
export interface AccentPaletteEntry {
  key: string;
  label: string;
  hex: string;
}

export interface AccentPaletteResponse {
  entries: AccentPaletteEntry[];
  defaultKey: string;
}

// V1-CUI-012: a typed setting's full record (V1-RMD-246's generic
// management surface) — only `value` and `rowVersion` are used by this
// screen, but the wire shape is the server's SettingRecordV1 as-is.
export interface SettingRecord {
  settingId: string;
  key: string;
  value: string;
  dataType: string;
  scope: string;
  moduleOwner: string;
  description: string | null;
  requiresRestart: boolean;
  active: boolean;
  updatedAt: string;
  rowVersion: number;
}
