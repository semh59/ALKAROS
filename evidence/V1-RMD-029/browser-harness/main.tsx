import React, { useState } from "react";
import { createRoot } from "react-dom/client";
import { BillSplitWorkspace } from "../../../src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace";
import type { BillSplitDesign, SaveSplitRequest } from "../../../src/Clients/PosTerminal/src/features/billing/models";
import "../../../src/Clients/PosTerminal/src/design-system/tokens.css";
import "../../../src/Clients/PosTerminal/src/design-system/primitives.css";
import "./harness.css";

const owners = [
  { kind: "Seat" as const, ownerId: "11111111-1111-1111-1111-111111111111", label: "Sandalye 1", secondaryLabel: "A-01 · Pencere" },
  { kind: "Seat" as const, ownerId: "22222222-2222-2222-2222-222222222222", label: "Sandalye 2", secondaryLabel: "A-01 · Koridor" },
  { kind: "Seat" as const, ownerId: "33333333-3333-3333-3333-333333333333", label: "Sandalye 3", secondaryLabel: "A-01" },
  { kind: "Person" as const, ownerId: "44444444-4444-4444-4444-444444444444", label: "Misafir 1", secondaryLabel: "Sandalyesiz kişi" },
];
const initial: BillSplitDesign = {
  billId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", billNumber: "H-1042", billStatus: "Open", currencyCode: "TRY",
  payableAmount: 910, taxTotal: 82.73, billRowVersion: 12, mode: "EqualByPerson", executionState: "DesignOnly",
  allowedCommands: ["SaveEqual", "SaveItems", "SaveAmounts", "SaveCustom", "Clear"],
  items: [
    { billItemId: "item-1", productName: "Taş fırın pizza", quantity: 2, grossAmount: 440, taxAmount: 40, rowVersion: 3 },
    { billItemId: "item-2", productName: "Izgara köfte", quantity: 1, grossAmount: 330, taxAmount: 30, rowVersion: 2 },
    { billItemId: "item-3", productName: "Ev ayranı", quantity: 2, grossAmount: 140, taxAmount: 12.73, rowVersion: 2 },
  ],
  allocations: owners.slice(0, 2).map((owner, index) => ({ allocationId: `allocation-${index}`, mode: "EqualByPerson", ownerKind: owner.kind, ownerId: owner.ownerId, legacyOwnerReference: null, billItemId: null, quantity: null, amount: 455, taxAmount: index ? 41.37 : 41.36, rowVersion: 2 })),
};

function App() {
  const [design, setDesign] = useState(initial);
  const save = async (request: SaveSplitRequest) => {
    await new Promise((resolve) => setTimeout(resolve, 220));
    const next = { ...design, billRowVersion: design.billRowVersion + 1, mode: request.mode, allocations: request.mode === "ByAmount" ? request.targets.map((target, index) => ({ allocationId: `amount-${index}`, mode: request.mode, ownerKind: target.owner.kind, ownerId: target.owner.ownerId, legacyOwnerReference: null, billItemId: null, quantity: null, amount: target.amount, taxAmount: 0, rowVersion: 1 })) : design.allocations };
    setDesign(next); return next;
  };
  const clear = async () => { const next = { ...design, billRowVersion: design.billRowVersion + 1, mode: "None", allocations: [] }; setDesign(next); return next; };
  return <div className="audit-frame"><header className="audit-header"><div><span>ALKAROS / KASA</span><strong>A-01 hesap bağlamı</strong></div><div><span className="audit-live">● Canlı</span><span>Terminal POS-01</span></div></header><BillSplitWorkspace state="ready" design={design} owners={owners} canMutate onRefresh={() => undefined} onSave={save} onClear={clear} lastUpdated="şimdi" /></div>;
}
createRoot(document.getElementById("root")!).render(<React.StrictMode><App /></React.StrictMode>);
