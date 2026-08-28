export type TableWorkspaceState =
  | "loading"
  | "ready"
  | "empty"
  | "error"
  | "offline"
  | "stale"
  | "unauthorized";

export type TableView = "map" | "list";
export type TableStatus = "Available" | "Occupied" | "Reserved" | "Cleaning" | "OutOfService";
export type FloorTableShape = "Rectangle" | "Round" | "Square";

export interface TableZone {
  zoneId: string;
  code: string;
  name: string;
  sortOrder: number;
  active: boolean;
  rowVersion: number;
}

export interface TableRecord {
  tableId: string;
  tableNumber: string;
  zoneId: string | null;
  capacity: number;
  active: boolean;
  status: TableStatus;
  currentOrderId: string | null;
  currentBillId: string | null;
  rowVersion: number;
  allowedCommands: readonly string[];
  occupiedSince?: string | null;
}

export interface FloorPlanSeat {
  seatId: string;
  number: number;
  label: string;
  x: number;
  y: number;
  rowVersion: number;
}

export interface FloorPlanTable {
  tableId: string;
  tableNumber: string;
  capacity: number;
  active: boolean;
  status: TableStatus;
  currentOrderId: string | null;
  currentBillId: string | null;
  allowedCommands: readonly string[];
  tableRowVersion: number;
  x: number | null;
  y: number | null;
  width: number | null;
  height: number | null;
  shape: FloorTableShape | null;
  rotationDegrees: 0 | 90 | 180 | 270 | null;
  layoutRowVersion: number;
  activeReservationId: string | null;
  reservationPartySize: number | null;
  reservedAt: string | null;
  reservationExpiresAt: string | null;
  mergeGroupId: string | null;
  isMergePrimary: boolean;
  seats: readonly FloorPlanSeat[];
}

export interface FloorPlan {
  zoneId: string;
  zoneCode: string;
  zoneName: string;
  canvasWidth: number;
  canvasHeight: number;
  rowVersion: number;
  tables: readonly FloorPlanTable[];
}

export interface SaveFloorPlanSeatInput {
  seatId: string;
  expectedRowVersion: number;
  number: number;
  label: string;
  x: number;
  y: number;
}

export interface SaveFloorPlanTableInput {
  tableId: string;
  expectedTableRowVersion: number;
  expectedLayoutRowVersion: number;
  x: number;
  y: number;
  width: number;
  height: number;
  shape: FloorTableShape;
  rotationDegrees: 0 | 90 | 180 | 270;
  seats: readonly SaveFloorPlanSeatInput[];
}

export interface SaveFloorPlanInput {
  expectedRowVersion: number;
  canvasWidth: number;
  canvasHeight: number;
  tables: readonly SaveFloorPlanTableInput[];
}

export interface FloorPlanWarning {
  code: string;
  tableId: string;
  message: string;
}

export interface SaveFloorPlanResult {
  floorPlan: FloorPlan;
  warnings: readonly FloorPlanWarning[];
}

export interface CreateZoneInput {
  code: string;
  name: string;
  sortOrder: number;
}

export interface CreateTableInput {
  tableNumber: string;
  zoneId: string | null;
  capacity: number;
}

export type TableAction =
  | "SetOccupied"
  | "SetAvailable"
  | "Reserve"
  | "CancelReservation"
  | "ClaimReservation"
  | "Transfer"
  | "Merge"
  | "Unmerge"
  | "SetCleaning"
  | "SetOutOfService";

export interface TableActionRequest {
  table: TableRecord;
  action: TableAction;
  reason?: string;
  targetTableId?: string;
  targetTableVersion?: number;
  participantTableIds?: readonly string[];
  participantTableVersions?: readonly { tableId: string; rowVersion: number }[];
  mergeGroupId?: string;
}

export interface TableWorkspaceProps {
  state: TableWorkspaceState;
  zones: readonly TableZone[];
  tables: readonly TableRecord[];
  canManage: boolean;
  selectedTableId?: string | null;
  onSelectTable: (tableId: string) => void;
  onRefresh: () => void | Promise<void>;
  onCreateZone?: (input: CreateZoneInput) => void | Promise<void>;
  onCreateTable?: (input: CreateTableInput) => void | Promise<void>;
  onAction?: (request: TableActionRequest) => void | Promise<void>;
  floorPlan?: FloorPlan | null;
  floorPlanBusy?: boolean;
  floorPlanError?: string;
  onSaveFloorPlan?: (zoneId: string, input: SaveFloorPlanInput) => SaveFloorPlanResult | Promise<SaveFloorPlanResult>;
  errorMessage?: string;
  lastUpdated?: string;
}

export const tableStatusLabels: Record<TableStatus, string> = {
  Available: "Müsait",
  Occupied: "Dolu",
  Reserved: "Rezerve",
  Cleaning: "Temizlik",
  OutOfService: "Servis dışı",
};

export const tableActionLabels: Partial<Record<TableAction, string>> = {
  SetOccupied: "Masayı aç",
  SetAvailable: "Müsait yap",
  Reserve: "Rezervasyon al",
  CancelReservation: "Rezervasyonu iptal et",
  ClaimReservation: "Rezervasyonu sahiplen",
  Transfer: "Masa değiştir",
  Merge: "Masaları birleştir",
  Unmerge: "Birleşimi ayır",
  SetCleaning: "Temizliğe al",
  SetOutOfService: "Servis dışı yap",
};

export const actionNeedsReason = (action: TableAction) =>
  action === "Reserve" || action === "CancelReservation" || action === "Transfer" || action === "Merge" || action === "Unmerge";

export const isClientExecutableAction = (action: TableAction) =>
  action !== "CancelReservation" && action !== "ClaimReservation";
