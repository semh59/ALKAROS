import { useCallback, useMemo, useState, type FormEvent } from "react";
import { Button, SelectField, TextField } from "../../design-system";
import { formatMoney, formatQuantity } from "../../format";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { createPurchasingClient, type PurchasingClient } from "./api";
import { Notice, PanelView, errorText, usePanel } from "./panel";
import {
  orderStatusLabel, orderStatuses, parseQuantity, type LookupItem, type LookupLocation, type NewOrderLine, type PurchaseOrder, type Supplier,
} from "./models";
import "./management-purchasing-production.css";

const t = managementText.purchasing;
type Line = { stockItemId: string; quantity: string; price: string };
const blankLine: Line = { stockItemId: "", quantity: "", price: "" };

export function PurchasingSection({ client }: ManagementSectionProps & { client?: PurchasingClient }) {
  const api = useMemo(() => client ?? createPurchasingClient(), [client]);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();
  const [busy, setBusy] = useState(false);
  const [statusFilter, setStatusFilter] = useState("");
  const [fields, setFields] = useState<Record<string, string>>({});
  const [lines, setLines] = useState<Line[]>([blankLine]);
  const [receiving, setReceiving] = useState<PurchaseOrder>();
  const [received, setReceived] = useState<Record<string, string>>({});
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [approved, setApproved] = useState(false);
  const field = (key: string) => fields[key] ?? "";
  const set = (key: string) => (event: { target: { value: string } }) => setFields((current) => ({ ...current, [key]: event.target.value }));

  const [suppliers, reloadSuppliers] = usePanel<readonly Supplier[]>(useCallback(() => api.listSuppliers(), [api]), t.loadFailed);
  const [orders, reloadOrders] = usePanel<readonly PurchaseOrder[]>(useCallback(() => api.listOrders(statusFilter), [api, statusFilter]), t.loadFailed);
  const [lookups] = usePanel<{ locations: readonly LookupLocation[]; items: readonly LookupItem[] }>(useCallback(
    () => Promise.all([api.listLocations(), api.listStockItems()]).then(([locations, items]) => ({ locations, items })),
    [api]), t.lookupFailed);


  async function act(work: () => Promise<void>, success: string, refresh: () => void, done?: () => void) {
    setBusy(true);
    try {
      await work();
      setNotice({ tone: "success", text: success });
      done?.();
      refresh();
    } catch (reason) {
      setNotice({ tone: "error", text: errorText(reason, t.actionFailed) });
    } finally {
      setBusy(false);
    }
  }
  const supplierName = (id: string) => suppliers.state === "ready" ? suppliers.data.find((supplier) => supplier.id === id)?.name ?? "—" : "—";
  const itemName = (id: string) => lookups.state === "ready" ? lookups.data.items.find((item) => item.id === id)?.name ?? "—" : "—";
  const updateLine = (index: number, patch: Partial<Line>) => setLines((current) => current.map((line, position) => position === index ? { ...line, ...patch } : line));

  function submitOrder(event: FormEvent) {
    event.preventDefault();
    if (lookups.state !== "ready") return;
    const built: NewOrderLine[] = [];
    for (const line of lines.filter((candidate) => candidate.stockItemId || candidate.quantity || candidate.price)) {
      const quantity = parseQuantity(line.quantity);
      const price = parseQuantity(line.price);
      const item = lookups.data.items.find((candidate) => candidate.id === line.stockItemId);
      if (quantity === null || price === null || !item) return setNotice({ tone: "error", text: t.invalidNumber });
      built.push({ stockItemId: item.id, orderedQuantity: quantity, unitCode: item.trackingUnitCode, unitPrice: price });
    }
    if (built.length === 0) return setNotice({ tone: "error", text: t.needLines });
    void act(() => api.createOrder({ orderNumber: field("o-number"), supplierId: field("o-supplier"), destinationLocationId: field("o-location"), lines: built, notes: field("o-notes") }),
      t.orderCreated, reloadOrders, () => { setLines([blankLine]); setFields({}); });
  }

  function submitReceipt(event: FormEvent) {
    event.preventDefault();
    if (!receiving) return;
    const items: { orderLineId: string; deliveredQuantity: number; varianceReason: string | null }[] = [];
    for (const line of receiving.lines) {
      const text = received[line.id] ?? String(line.openQuantity);
      const delivered = parseQuantity(text);
      if (delivered === null) return setNotice({ tone: "error", text: t.invalidNumber });
      if (delivered > 0) items.push({ orderLineId: line.id, deliveredQuantity: delivered, varianceReason: (reasons[line.id] ?? "").trim() || null });
    }
    void act(() => api.receiveGoods(receiving.id, { receiptNumber: field("r-number"), items, isManagerApproved: approved, notes: field("r-notes") }),
      t.receiptSaved, reloadOrders, () => { setReceiving(undefined); setReceived({}); setReasons({}); setApproved(false); });
  }

  return <div className="mpp">
    <Notice notice={notice} />

    <PanelView title={t.suppliersHeading} loading={t.loading} failed={t.loadFailed} panel={suppliers} onRetry={reloadSuppliers}>
      {(list) => <div>
        {list.length === 0 ? <p className="mpp__empty">{t.empty}</p> : <table>
          <thead><tr><th>{t.code}</th><th>{t.name}</th><th>{t.status}</th><th>{t.actions}</th></tr></thead>
          <tbody>{list.map((supplier) => <tr key={supplier.id}>
            <td>{supplier.code}</td><td>{supplier.name}</td><td>{supplier.active ? t.active : t.passive}</td>
            <td><Button variant="quiet" disabled={busy} onClick={() => void act(() => api.setSupplierActive(supplier.id, !supplier.active), t.supplierUpdated, reloadSuppliers)}>
              {supplier.active ? t.deactivate : t.activate}</Button></td></tr>)}</tbody>
        </table>}
        <form className="mpp__form" aria-label={t.newSupplier} onSubmit={(event) => {
          event.preventDefault();
          void act(() => api.createSupplier({
            code: field("s-code"), name: field("s-name"), taxNumber: field("s-tax") || null, taxOffice: field("s-office") || null,
            phone: field("s-phone") || null, email: field("s-email") || null,
          }), t.supplierAdded, reloadSuppliers, () => setFields({}));
        }}>
          <h4>{t.newSupplier}</h4>
          <TextField id="s-code" label={t.code} value={field("s-code")} onChange={set("s-code")} />
          <TextField id="s-name" label={t.name} value={field("s-name")} onChange={set("s-name")} />
          <TextField id="s-tax" label={t.taxNumber} value={field("s-tax")} onChange={set("s-tax")} />
          <TextField id="s-office" label={t.taxOffice} value={field("s-office")} onChange={set("s-office")} />
          <TextField id="s-phone" label={t.phone} value={field("s-phone")} onChange={set("s-phone")} />
          <TextField id="s-email" label={t.email} value={field("s-email")} onChange={set("s-email")} />
          <Button type="submit" disabled={busy}>{t.add}</Button>
        </form>
      </div>}
    </PanelView>

    <PanelView title={t.ordersHeading} loading={t.loading} failed={t.loadFailed} panel={orders} onRetry={reloadOrders}>
      {(list) => <div>
        <SelectField id="o-filter" label={t.statusFilter} value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
          <option value="">{t.allStatuses}</option>
          {orderStatuses.map((status) => <option key={status} value={status}>{orderStatusLabel(status)}</option>)}
        </SelectField>
        {list.length === 0 ? <p className="mpp__empty">{t.empty}</p> : <table>
          <thead><tr><th>{t.orderNumber}</th><th>{t.supplier}</th><th>{t.status}</th><th>{t.total}</th><th>{t.actions}</th></tr></thead>
          <tbody>{list.map((order) => <tr key={order.id}>
            <td>{order.orderNumber}</td><td>{supplierName(order.supplierId)}</td><td>{orderStatusLabel(order.status)}</td><td>{formatMoney(order.totalAmount, order.currency)}</td>
            <td className="mpp__actions">
              {order.status === "Draft" && <Button variant="quiet" disabled={busy} onClick={() => void act(() => api.submitOrder(order.id), t.orderSubmitted, reloadOrders)}>{t.submit}</Button>}
              {(order.status === "Submitted" || order.status === "PartiallyReceived") && <Button variant="quiet" disabled={busy} onClick={() => { setNotice(undefined); setReceiving(order); }}>{t.receive}</Button>}
              {(order.status === "Draft" || order.status === "Submitted") && <Button variant="quiet" disabled={busy} onClick={() => void act(() => api.cancelOrder(order.id), t.orderCancelled, reloadOrders)}>{t.cancel}</Button>}
            </td></tr>)}</tbody>
        </table>}

        {receiving && <form className="mpp__form" aria-label={`${t.receive} ${receiving.orderNumber}`} onSubmit={submitReceipt}>
          <h4>{t.receive} · {receiving.orderNumber}</h4>
          <TextField id="r-number" label={t.receiptNumber} value={field("r-number")} onChange={set("r-number")} />
          {receiving.lines.filter((line) => line.openQuantity > 0).map((line) => <fieldset key={line.id} className="mpp__line">
            <legend>{itemName(line.stockItemId)} · {t.open}: {formatQuantity(line.openQuantity)} {line.unitCode}</legend>
            <TextField id={`r-${line.id}`} label={t.delivered} inputMode="decimal" value={received[line.id] ?? String(line.openQuantity)} onChange={(event) => setReceived((current) => ({ ...current, [line.id]: event.target.value }))} />
            <TextField id={`v-${line.id}`} label={t.variance} value={reasons[line.id] ?? ""} onChange={(event) => setReasons((current) => ({ ...current, [line.id]: event.target.value }))} />
          </fieldset>)}
          <label className="mpp__check"><input type="checkbox" checked={approved} onChange={(event) => setApproved(event.target.checked)} /> {t.managerApproved}</label>
          <TextField id="r-notes" label={t.notes} value={field("r-notes")} onChange={set("r-notes")} />
          <div className="mpp__actions">
            <Button type="submit" disabled={busy}>{t.confirmReceipt}</Button>
            <Button type="button" variant="secondary" onClick={() => setReceiving(undefined)}>{t.close}</Button>
          </div>
        </form>}

        <PanelView title={t.newOrder} loading={t.loading} failed={t.lookupFailed} panel={lookups} onRetry={() => undefined}>
          {(lookup) => <form className="mpp__form" aria-label={t.newOrder} onSubmit={submitOrder}>
            <TextField id="o-number" label={t.orderNumber} value={field("o-number")} onChange={set("o-number")} />
            <SelectField id="o-supplier" label={t.supplier} value={field("o-supplier")} onChange={set("o-supplier")}>
              <option value="">{t.pick}</option>
              {suppliers.state === "ready" && suppliers.data.filter((supplier) => supplier.active).map((supplier) => <option key={supplier.id} value={supplier.id}>{supplier.name}</option>)}
            </SelectField>
            <SelectField id="o-location" label={t.destination} value={field("o-location")} onChange={set("o-location")}>
              <option value="">{t.pick}</option>
              {lookup.locations.map((location) => <option key={location.id} value={location.id}>{location.name}</option>)}
            </SelectField>
            {lines.map((line, index) => <fieldset key={index} className="mpp__line">
              <legend>{t.stockItem} {index + 1}</legend>
              <SelectField id={`l-item-${index}`} label={t.stockItem} value={line.stockItemId} onChange={(event) => updateLine(index, { stockItemId: event.target.value })}>
                <option value="">{t.pick}</option>
                {lookup.items.map((item) => <option key={item.id} value={item.id}>{item.name} ({item.trackingUnitCode})</option>)}
              </SelectField>
              <TextField id={`l-qty-${index}`} label={t.quantity} inputMode="decimal" value={line.quantity} onChange={(event) => updateLine(index, { quantity: event.target.value })} />
              <TextField id={`l-price-${index}`} label={t.unitPrice} inputMode="decimal" value={line.price} onChange={(event) => updateLine(index, { price: event.target.value })} />
              {lines.length > 1 && <Button type="button" variant="quiet" onClick={() => setLines((current) => current.filter((_, position) => position !== index))}>{t.removeLine}</Button>}
            </fieldset>)}
            <TextField id="o-notes" label={t.notes} value={field("o-notes")} onChange={set("o-notes")} />
            <div className="mpp__actions">
              <Button type="button" variant="secondary" onClick={() => setLines((current) => [...current, blankLine])}>{t.addLine}</Button>
              <Button type="submit" disabled={busy}>{t.createOrder}</Button>
            </div>
          </form>}
        </PanelView>
      </div>}
    </PanelView>
  </div>;
}
