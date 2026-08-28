import React, { useState } from "react";
import { createRoot } from "react-dom/client";
import { TableWorkspace } from "../../../src/Clients/PosTerminal/src/features/tables/TableWorkspace";
import { tableActionLabels, type FloorPlan, type SaveFloorPlanInput, type TableActionRequest, type TableRecord } from "../../../src/Clients/PosTerminal/src/features/tables/models";
import "../../../src/Clients/PosTerminal/src/design-system/tokens.css";
import "../../../src/Clients/PosTerminal/src/design-system/primitives.css";
import "./harness.css";

const initialPlan: FloorPlan = {
  zoneId: "11111111-1111-1111-1111-111111111111",
  zoneCode: "SALON",
  zoneName: "Ana Salon",
  canvasWidth: 1280,
  canvasHeight: 800,
  rowVersion: 6,
  tables: [
    table("A-01", "Occupied", 108, 128, 176, 104, "Rectangle", 4, { order: "ORD-1042", bill: "BIL-1042", seats: 4 }),
    table("A-02", "Reserved", 388, 112, 132, 132, "Round", 2, { reservation: true, seats: 2 }),
    table("A-03", "Available", 660, 122, 152, 96, "Rectangle", 4, { seats: 4 }),
    table("B-01", "Occupied", 188, 424, 144, 144, "Round", 4, { order: "ORD-1088", seats: 4, merge: "merge-1", primary: true }),
    table("B-02", "Occupied", 360, 438, 152, 96, "Rectangle", 2, { order: "ORD-1088", seats: 2, merge: "merge-1" }),
    table("B-03", "Cleaning", 690, 430, 152, 96, "Rectangle", 2, { seats: 2 }),
    table("P-01", "OutOfService", 1000, 278, 132, 132, "Square", 1, { seats: 0 }),
  ],
};

function table(
  tableNumber: string,
  status: FloorPlan["tables"][number]["status"],
  x: number,
  y: number,
  width: number,
  height: number,
  shape: FloorPlan["tables"][number]["shape"],
  capacity: number,
  context: { order?: string; bill?: string; reservation?: boolean; seats: number; merge?: string; primary?: boolean },
): FloorPlan["tables"][number] {
  const tableId = crypto.randomUUID();
  return {
    tableId,
    tableNumber,
    capacity,
    active: status !== "OutOfService",
    status,
    currentOrderId: context.order ?? null,
    currentBillId: context.bill ?? null,
    tableRowVersion: 3,
    x, y, width, height, shape,
    rotationDegrees: 0,
    layoutRowVersion: 2,
    activeReservationId: context.reservation ? crypto.randomUUID() : null,
    reservationPartySize: context.reservation ? 2 : null,
    reservedAt: context.reservation ? new Date().toISOString() : null,
    reservationExpiresAt: null,
    mergeGroupId: context.merge ?? null,
    isMergePrimary: context.primary ?? false,
    seats: Array.from({ length: context.seats }, (_, index) => ({
      seatId: crypto.randomUUID(),
      number: index + 1,
      label: `Sandalye ${index + 1}`,
      x: x + (index % 2 ? width : 0),
      y: y + (index < 2 ? 0 : height),
      rowVersion: 2,
    })),
    allowedCommands: status === "Available" ? ["SetOccupied", "Reserve"] : status === "Occupied" ? ["Transfer", "Merge"] : [],
  };
}

function App() {
  const [plan, setPlan] = useState(initialPlan);
  const [selected, setSelected] = useState(plan.tables[0].tableId);
  const [actionMessage, setActionMessage] = useState<string>();
  const records: TableRecord[] = plan.tables.map((item) => ({
    tableId: item.tableId,
    tableNumber: item.tableNumber,
    zoneId: plan.zoneId,
    capacity: item.capacity,
    active: item.active,
    status: item.status,
    currentOrderId: item.currentOrderId,
    currentBillId: item.currentBillId,
    rowVersion: item.tableRowVersion,
    allowedCommands: item.allowedCommands,
    occupiedSince: item.status === "Occupied" ? new Date(Date.now() - 74 * 60_000).toISOString() : null,
  }));
  const save = async (_zoneId: string, input: SaveFloorPlanInput) => {
    await new Promise((resolve) => setTimeout(resolve, 250));
    const next: FloorPlan = {
      ...plan,
      rowVersion: plan.rowVersion + 1,
      canvasWidth: input.canvasWidth,
      canvasHeight: input.canvasHeight,
      tables: plan.tables.map((item) => {
        const saved = input.tables.find((candidate) => candidate.tableId === item.tableId)!;
        return { ...item, ...saved, tableRowVersion: item.tableRowVersion, layoutRowVersion: item.layoutRowVersion + 1, seats: saved.seats.map((seat) => ({ ...seat, rowVersion: seat.expectedRowVersion + 1 })) };
      }),
    };
    setPlan(next);
    return { floorPlan: next, warnings: [] };
  };
  const execute = async (request: TableActionRequest) => {
    await new Promise((resolve) => setTimeout(resolve, 180));
    setActionMessage(`${request.table.tableNumber}: ${tableActionLabels[request.action]} tamamlandı.`);
  };
  return <div className="audit-frame">
    <header className="audit-header"><div><span>ALKAROS / OPERASYON</span><strong>Salon yönetimi</strong></div><div><span className="audit-live">● Canlı</span><span>Terminal POS-01</span></div></header>
    {actionMessage && <div className="audit-action" role="status">{actionMessage}</div>}
    <TableWorkspace
      state="ready"
      zones={[{ zoneId: plan.zoneId, code: plan.zoneCode, name: plan.zoneName, sortOrder: 1, active: true, rowVersion: 2 }]}
      tables={records}
      canManage
      selectedTableId={selected}
      onSelectTable={setSelected}
      onRefresh={() => undefined}
      onCreateZone={() => undefined}
      onCreateTable={() => undefined}
      onAction={execute}
      floorPlan={plan}
      onSaveFloorPlan={save}
      lastUpdated="şimdi"
    />
  </div>;
}

createRoot(document.getElementById("root")!).render(<React.StrictMode><App /></React.StrictMode>);
