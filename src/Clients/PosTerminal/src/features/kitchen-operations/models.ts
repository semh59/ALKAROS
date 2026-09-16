// Enum label maps live in the central catalog (finding F-7).
import {
  backupStatusLabels,
  healthStatusLabels,
  itemStatusLabels,
  ticketStatusLabels,
} from "../../strings";

export { backupStatusLabels, healthStatusLabels, itemStatusLabels, ticketStatusLabels };

export type KitchenWorkspaceState =
  | "loading"
  | "ready"
  | "empty"
  | "busy"
  | "error"
  | "offline"
  | "stale"
  | "unauthorized"
  | "conflict";

export interface KitchenTicketItem {
  id: string;
  orderItemId: string;
  productId: string;
  productName: string;
  quantity: number;
  modifiers: string | null;
  notes: string | null;
  status: "Queued" | "Preparing" | "Ready" | "Served" | "Cancelled";
  rowVersion: number;
  createdAt: string;
  updatedAt: string | null;
  readyAt: string | null;
  servedAt: string | null;
  cancelledAt: string | null;
  // V1-KDS-001: KitchenTicketItemV1 has carried this since V1-RMD-137, the
  // client just never read it — an id-check prompt belongs on-screen, not
  // only on the printed ticket (EscPosTicketFormatter already prints it).
  isAgeRestricted: boolean;
  // V1-RMD-220: found by an independent audit (2026-09-16) - the domain
  // model has carried this since the course system shipped (V1-WTR-025),
  // but it was never in this DTO, so the screen could not tell a
  // deliberately-held course item apart from a normal Queued one.
  isHeld: boolean;
}

export interface KitchenTicket {
  id: string;
  orderId: string;
  ticketNumber: string;
  stationId: string;
  status: "Queued" | "Accepted" | "Preparing" | "Ready" | "Cancelled";
  rowVersion: number;
  createdAt: string;
  updatedAt: string | null;
  acceptedAt: string | null;
  readyAt: string | null;
  cancelledAt: string | null;
  targetPrepMinutes: number;
  items: readonly KitchenTicketItem[];
  // V1-KIT-012/V1-KDS-005: resolved fresh from orders.orders + table_mgmt.tables
  // on the backend, never fabricated here. Both null for a table-less
  // order (takeaway/bar tab).
  tableId: string | null;
  tableNumber: string | null;
}

export interface KitchenPrinter {
  id: string;
  name: string;
  stationId: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface KitchenPrinterRoute {
  id: string;
  routeLevel: string;
  printerId: string;
  itemId: string | null;
  productId: string | null;
  categoryId: string | null;
  specialDate: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

/** A minimal read of catalog.categories - just enough for the routing form's own dropdown. */
export interface KitchenCategory {
  id: string;
  name: string;
}

export interface KitchenUnknownDelivery {
  id: string;
  printJobId: string;
  ticketId: string;
  printerId: string;
  status: "InFlight" | "Printed" | "Unknown" | "ReprintApproved" | "ReprintRejected" | "Reprinted";
  attemptNumber: number;
  isReprint: boolean;
  operatorReason: string | null;
  crashReason: string | null;
  createdAt: string;
  deliveredAt: string | null;
  resolvedAt: string | null;
  rowVersion: number;
}

export interface KitchenBackup {
  backupId: string;
  backupType: string;
  fileSizeBytes: number;
  status: "InProgress" | "Completed" | "Failed";
  errorMessage: string | null;
  startedAt: string;
  completedAt: string | null;
  retentionDays: number;
}

export interface KitchenHealthSnapshot {
  snapshotId: string;
  databaseStatus: "Healthy" | "Degraded" | "Unhealthy";
  diskStatus: "Healthy" | "Degraded" | "Unhealthy";
  lastBackupStatus: "Healthy" | "Degraded" | "Unhealthy";
  freeDiskBytes: number;
  databaseSizeBytes: number;
  capturedAt: string;
}

export interface KitchenData {
  tickets: readonly KitchenTicket[];
  printers: readonly KitchenPrinter[];
  routes: readonly KitchenPrinterRoute[];
  categories: readonly KitchenCategory[];
  unknownDeliveries: readonly KitchenUnknownDelivery[];
  health: KitchenHealthSnapshot | null;
  backups: readonly KitchenBackup[];
  // V1-KIT-010/V1-KDS-004: kitchen.live_sync_enabled — off by default,
  // silently skips the waiter ready-notification and KitchenState mirror
  // when off. Shown so the screen doesn't lie about what it's doing.
  liveSyncEnabled: boolean;
  // V1-KIT-013/V1-KDS-006: kitchen.dense_mode_threshold — the open-item
  // count at or above which the screen auto-switches to dense mode.
  // Replaces the workspace's own former hardcoded constant (default 9,
  // same number, so an untouched deployment never changes behavior).
  denseModeThreshold: number;
}

/** V1-KIT-008/V1-KDS-002: the result of 86-ing a product from this screen. */
export interface ProductAvailabilitySuspended {
  productId: string;
  isAvailable: boolean;
  // True when the product was still on active sale the instant before this
  // suspend — the backend then also wrote an informational
  // authorization_grants row for a manager to find (V1-KIT-008's Goal:
  // this is a durable audit record, not a live push).
  planConflict: boolean;
}

/**
 * V1-KIT-014/V1-KDS-009: one station's timing over a report window. Mean
 * AND median are both carried — a real vendor's own docs (Fresh KDS)
 * warn an average alone can hide extreme values. `targetMinutes` is
 * honestly the backend's fixed global default, not a per-product
 * estimate.
 */
export interface StationPerformance {
  stationId: string;
  completedTicketCount: number;
  averageMinutes: number;
  medianMinutes: number;
  targetMinutes: number;
  targetOverrunPercentage: number;
}

export interface HourlyVolume {
  hourStart: string;
  completedTicketCount: number;
}

export interface KitchenPerformanceReport {
  from: string;
  to: string;
  stations: readonly StationPerformance[];
  hourlyVolume: readonly HourlyVolume[];
}

export interface KitchenWorkspaceProps {
  state: KitchenWorkspaceState;
  stationId: string;
  data: KitchenData;
  // V1-IAM-028/V1-KDS-001: two distinct grants, not one. `canAdvance` is
  // `kitchen.advance` — every FOH role has it AND so does the narrow
  // "Mutfak Personeli" (kitchen-staff) role; it only allows moving a
  // ticket/item one stage forward. `canOperate` is `orders.send` — held by
  // FOH roles but NOT kitchen-staff; it is required for anything that is
  // not a forward step (cancel/"sorun bildir"). A kitchen-staff session has
  // canAdvance=true, canOperate=false. Every existing FOH role has both.
  canAdvance: boolean;
  canOperate: boolean;
  // V1-RMD-200: `kitchen.reprint` — a real, separate permission from
  // `canOperate` (`orders.send`). Independent audit (2026-09-14) found
  // this was previously fed `canOperate` directly, showing the
  // reprint-approval panel to waiter/cashier sessions (which hold
  // orders.send but not kitchen.reprint) — the backend correctly
  // rejected them with 403, but the UI misled the user into trying.
  canManageReprints: boolean;
  // V1-RMD-200: `kitchen.routing.manage` — same class of bug as
  // canManageReprints above, same audit. Held only by manager and
  // kitchen-chef, NOT by orders.send holders in general (not even
  // supervisor).
  canManageRouting: boolean;
  // V1-IAM-029/V1-KDS-002: `kitchen.availability.suspend` - held outright
  // only by the kitchen-chef ("Mutfak Sefi") role today (migration 110
  // also grants it to manager, so V1-KIT-008's endpoint is testable before
  // this role existed). Neither `canAdvance` nor `canOperate` implies this.
  canSuspendAvailability: boolean;
  // V1-KIT-014/V1-KDS-009: `reports.view` — held only by supervisor/
  // manager today (same gate the audit-log endpoints already use).
  // Neither `canAdvance`/`canOperate`/`canManageReprints` implies this;
  // whether kitchen-chef should hold it is a separate, undecided question.
  canViewReports: boolean;
  onRefresh: () => void | Promise<void>;
  onTransitionItem?: (ticket: KitchenTicket, item: KitchenTicketItem, targetState: KitchenTicketItem["status"]) => void | Promise<void>;
  // V1-KIT-009/V1-KDS-003: reverses an item's most recent transition within
  // its short undo window. Gated by canAdvance only, same as onTransitionItem.
  onUndoItem?: (ticket: KitchenTicket, item: KitchenTicketItem) => void | Promise<void>;
  onTransitionTicket?: (ticket: KitchenTicket, targetState: KitchenTicket["status"], reason?: string) => void | Promise<void>;
  onApproveReprint?: (delivery: KitchenUnknownDelivery, reason: string) => void | Promise<void>;
  onRejectReprint?: (delivery: KitchenUnknownDelivery, reason: string) => void | Promise<void>;
  onCreateCategoryRoute?: (categoryId: string, printerId: string) => void | Promise<void>;
  // V1-KIT-008/V1-KDS-002: 86 a product. Present whenever a cashier session
  // exists at all — visibility of the *button* is gated in the workspace
  // itself (locked, not hidden, for a non-chef session, per this task's own
  // acceptance evidence), the actual call is gated server-side regardless.
  onSuspendProductAvailability?: (productId: string) => Promise<ProductAvailabilitySuspended>;
  // V1-KIT-014/V1-KDS-009: from/to as ISO-8601 strings, matching the
  // backend's own query parameter shape exactly - no client-side date
  // parsing/reformatting needed.
  onLoadPerformanceReport?: (from: string, to: string) => Promise<KitchenPerformanceReport>;
  errorMessage?: string;
  lastUpdated?: string;
}

export function healthStatusLabel(status: KitchenHealthSnapshot["databaseStatus"] | null | undefined): string {
  return status ? healthStatusLabels[status] : "Bilinmiyor";
}
