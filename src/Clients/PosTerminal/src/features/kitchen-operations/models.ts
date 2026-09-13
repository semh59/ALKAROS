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
  canManageReprints: boolean;
  onRefresh: () => void | Promise<void>;
  onTransitionItem?: (ticket: KitchenTicket, item: KitchenTicketItem, targetState: KitchenTicketItem["status"]) => void | Promise<void>;
  // V1-KIT-009/V1-KDS-003: reverses an item's most recent transition within
  // its short undo window. Gated by canAdvance only, same as onTransitionItem.
  onUndoItem?: (ticket: KitchenTicket, item: KitchenTicketItem) => void | Promise<void>;
  onTransitionTicket?: (ticket: KitchenTicket, targetState: KitchenTicket["status"], reason?: string) => void | Promise<void>;
  onApproveReprint?: (delivery: KitchenUnknownDelivery, reason: string) => void | Promise<void>;
  onRejectReprint?: (delivery: KitchenUnknownDelivery, reason: string) => void | Promise<void>;
  onCreateCategoryRoute?: (categoryId: string, printerId: string) => void | Promise<void>;
  errorMessage?: string;
  lastUpdated?: string;
}

export function healthStatusLabel(status: KitchenHealthSnapshot["databaseStatus"] | null | undefined): string {
  return status ? healthStatusLabels[status] : "Bilinmiyor";
}
