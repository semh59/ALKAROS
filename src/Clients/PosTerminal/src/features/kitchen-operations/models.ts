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
  unknownDeliveries: readonly KitchenUnknownDelivery[];
  health: KitchenHealthSnapshot | null;
  backups: readonly KitchenBackup[];
}

export interface KitchenWorkspaceProps {
  state: KitchenWorkspaceState;
  stationId: string;
  data: KitchenData;
  canOperate: boolean;
  canManageReprints: boolean;
  onRefresh: () => void | Promise<void>;
  onTransitionItem?: (ticket: KitchenTicket, item: KitchenTicketItem, targetState: KitchenTicketItem["status"]) => void | Promise<void>;
  onTransitionTicket?: (ticket: KitchenTicket, targetState: KitchenTicket["status"], reason?: string) => void | Promise<void>;
  onApproveReprint?: (delivery: KitchenUnknownDelivery, reason: string) => void | Promise<void>;
  onRejectReprint?: (delivery: KitchenUnknownDelivery, reason: string) => void | Promise<void>;
  errorMessage?: string;
  lastUpdated?: string;
}

export function healthStatusLabel(status: KitchenHealthSnapshot["databaseStatus"] | null | undefined): string {
  return status ? healthStatusLabels[status] : "Bilinmiyor";
}
