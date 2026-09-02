export type BillSplitWorkspaceState = "loading" | "ready" | "error" | "offline" | "stale" | "unauthorized";
export type SplitOwnerKind = "Seat" | "Person";
export type SplitMode = "EqualByPerson" | "ByItem" | "ByAmount";

export interface SplitOwnerOption {
  kind: SplitOwnerKind;
  ownerId: string;
  label: string;
  secondaryLabel?: string;
}

export interface BillSplitItem {
  billItemId: string;
  productName: string;
  quantity: number;
  grossAmount: number;
  taxAmount: number;
  rowVersion: number;
}

export interface BillSplitAllocation {
  allocationId: string;
  mode: string;
  ownerKind: string;
  ownerId: string | null;
  legacyOwnerReference: string | null;
  billItemId: string | null;
  quantity: number | null;
  amount: number;
  taxAmount: number;
  rowVersion: number;
}

export interface BillSplitDesign {
  billId: string;
  billNumber: string;
  billStatus: string;
  currencyCode: string;
  payableAmount: number;
  taxTotal: number;
  billRowVersion: number;
  mode: string;
  executionState: string;
  allowedCommands: readonly string[];
  items: readonly BillSplitItem[];
  allocations: readonly BillSplitAllocation[];
}

export interface SplitOwnerRequest { kind: SplitOwnerKind; ownerId: string }
export interface AllocationVersionRequest { allocationId: string; rowVersion: number }
export interface ItemSplitTarget { owner: SplitOwnerRequest; billItemId: string; quantity: number }
export interface AmountSplitTarget { owner: SplitOwnerRequest; amount: number }

export type SaveSplitRequest =
  | { mode: "EqualByPerson"; owners: readonly SplitOwnerRequest[] }
  | { mode: "ByItem"; targets: readonly ItemSplitTarget[] }
  | { mode: "ByAmount"; targets: readonly AmountSplitTarget[] };

export interface BillSplitWorkspaceProps {
  state: BillSplitWorkspaceState;
  design: BillSplitDesign | null;
  owners: readonly SplitOwnerOption[];
  canMutate: boolean;
  onRefresh: () => void | Promise<void>;
  onSave: (request: SaveSplitRequest, design: BillSplitDesign) => BillSplitDesign | Promise<BillSplitDesign>;
  onClear: (design: BillSplitDesign) => BillSplitDesign | Promise<BillSplitDesign>;
  errorMessage?: string;
  lastUpdated?: string;
}

// Enum label maps live in the central catalog (finding F-7).
export { modeLabels } from "../../strings";
