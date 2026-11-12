import { tablePlanText, tableStatusLabels } from "../../strings";
import type { TableRecord } from "./models";

export type PlanKey = "free" | "busy" | "ask" | "pay" | "rez" | "clean" | "off";

export interface PlanStatus {
  key: PlanKey;
  label: string;
  /** Shape cue so the state is readable without colour. */
  icon: string;
}

const icons: Record<PlanKey, string> = { free: "○", busy: "●", ask: "⚠", pay: "₺", rez: "◔", clean: "✦", off: "⊘" };
const labels: Record<PlanKey, string> = {
  free: tableStatusLabels.Available,
  busy: tableStatusLabels.Occupied,
  ask: tablePlanText.ask,
  pay: tablePlanText.pay,
  rez: tableStatusLabels.Reserved,
  clean: tableStatusLabels.Cleaning,
  off: tableStatusLabels.OutOfService,
};

export const planLegend = (Object.keys(icons) as PlanKey[]).map((key) => ({ key, icon: icons[key], label: labels[key] }));

/** A table is "asking" only when the till's own list holds a check sent from it that has no bill yet. */
export function planStatus(table: Pick<TableRecord, "tableId" | "active" | "status" | "currentBillId">, awaitingCheckTableIds?: ReadonlySet<string>): PlanStatus {
  const status = (key: PlanKey): PlanStatus => ({ key, label: labels[key], icon: icons[key] });
  if (!table.active || table.status === "OutOfService") return status("off");
  if (table.status === "Reserved") return status("rez");
  if (table.currentBillId) return status("pay");
  if (awaitingCheckTableIds?.has(table.tableId)) return status("ask");
  if (table.status === "Cleaning") return status("clean");
  if (table.status === "Occupied") return status("busy");
  return status("free");
}

export function attentionGroups(tables: readonly TableRecord[], awaitingCheckTableIds?: ReadonlySet<string>) {
  const groups: { key: "ask" | "pay"; label: string; icon: string; tables: TableRecord[] }[] = [
    { key: "ask", label: tablePlanText.attentionAsk, icon: icons.ask, tables: [] },
    { key: "pay", label: tablePlanText.attentionPay, icon: icons.pay, tables: [] },
  ];
  for (const table of tables) {
    const key = planStatus(table, awaitingCheckTableIds).key;
    if (key === "ask" || key === "pay") groups.find((group) => group.key === key)!.tables.push(table);
  }
  return groups.filter((group) => group.tables.length > 0);
}
