import { useCallback, useEffect, useMemo, useState, type FormEvent, type ReactNode } from "react";
import { Button, SelectField, StateMessage, TextField } from "../../design-system";
import { formatQuantity } from "../../format";
import { commonActions, managementText } from "../../strings";
import { createStockClient, type StockClient } from "./stockApi";
import {
  itemTypeLabel, itemTypes, locationTypeLabel, locationTypes, parseQuantity, wasteSourceLabel, wasteSources,
  type CriticalStockReport, type StockItem, type StockLocation, type VarianceReport,
} from "./models";
import { errorText } from "../management/panel";
import "./management-stock.css";

const t = managementText.stock;
const today = () => new Date().toLocaleDateString("sv-SE");

type Panel<T> = { state: "loading" } | { state: "error"; message: string } | { state: "ready"; data: T };

function usePanel<T>(load: () => Promise<T>): [Panel<T>, () => void] {
  const [panel, setPanel] = useState<Panel<T>>({ state: "loading" });
  const run = useCallback(() => {
    setPanel({ state: "loading" });
    load().then((data) => setPanel({ state: "ready", data }), (reason) => setPanel({ state: "error", message: errorText(reason, t.loadFailed) }));
  }, [load]);
  useEffect(run, [run]);
  return [panel, run];
}

function PanelView<T>({ title, panel, onRetry, children }: { title: string; panel: Panel<T>; onRetry: () => void; children: (data: T) => ReactNode }) {
  return <section className="stock__panel" aria-label={title}>
    <h3>{title}</h3>
    {panel.state === "loading" && <p aria-busy="true">{t.loading}</p>}
    {panel.state === "error" && <StateMessage tone="error" title={t.loadFailed} actions={<Button variant="secondary" onClick={onRetry}>{commonActions.retry}</Button>}><p>{panel.message}</p></StateMessage>}
    {panel.state === "ready" && children(panel.data)}
  </section>;
}

const Empty = () => <p className="stock__empty">{t.empty}</p>;
const stat = (value: number) => formatQuantity(value);

type RowAction = { kind: "count" | "waste" | "reorder"; item: StockItem };

export function StockSection({ capabilities, client }: { capabilities: ReadonlySet<string>; client?: StockClient }) {
  const api = useMemo(() => client ?? createStockClient(), [client]);
  const canManage = capabilities.has("inventory.manage");
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();
  const [busy, setBusy] = useState(false);

  const [critical, reloadCritical] = usePanel<CriticalStockReport>(useCallback(() => api.getCriticalStock(), [api]));
  const [from, setFrom] = useState(today);
  const [to, setTo] = useState(today);
  const [range, setRange] = useState({ from: today(), to: today() });
  const [variance, reloadVariance] = usePanel<VarianceReport>(useCallback(() => api.getVariance(range.from, range.to), [api, range]));
  const [items, reloadItems] = usePanel<readonly StockItem[]>(useCallback(() => canManage ? api.listItems() : Promise.resolve([]), [api, canManage]));
  const [locations, reloadLocations] = usePanel<readonly StockLocation[]>(useCallback(() => canManage ? api.listLocations() : Promise.resolve([]), [api, canManage]));

  const [action, setAction] = useState<RowAction>();
  const [fields, setFields] = useState<Record<string, string>>({});
  const set = (key: string) => (event: { target: { value: string } }) => setFields((current) => ({ ...current, [key]: event.target.value }));
  const field = (key: string, fallback = "") => fields[key] ?? fallback;

  async function act(work: () => Promise<void>, success: string, refresh: () => void) {
    setBusy(true);
    try {
      await work();
      setNotice({ tone: "success", text: success });
      setAction(undefined);
      refresh();
    } catch (reason) {
      setNotice({ tone: "error", text: errorText(reason, t.actionFailed) });
    } finally {
      setBusy(false);
    }
  }
  const refreshStock = () => { reloadItems(); reloadCritical(); };
  const invalid = () => setNotice({ tone: "error", text: t.invalidNumber });
  const openAction = (next: RowAction) => {
    setNotice(undefined);
    setFields(next.kind === "reorder" ? { reorder: next.item.reorderPoint === null ? "" : String(next.item.reorderPoint) } : { location: next.item.defaultLocationId ?? "", source: "Manual" });
    setAction(next);
  };

  function submitAction(event: FormEvent) {
    event.preventDefault();
    if (!action) return;
    const { kind, item } = action;
    if (kind === "reorder") {
      const text = field("reorder").trim();
      const value = text === "" ? null : parseQuantity(text);
      if (text !== "" && value === null) return invalid();
      return void act(() => api.setReorderPoint(item.id, value), t.reorderSaved, refreshStock);
    }
    const quantity = parseQuantity(field("quantity"));
    if (quantity === null) return invalid();
    if (!field("location")) return setNotice({ tone: "error", text: t.pickLocation });
    if (kind === "count") return void act(() => api.recordCount(item.id, field("location"), quantity, field("notes")), t.countSaved, refreshStock);
    void act(() => api.recordWaste(item.id, {
      stockLocationId: field("location"), wasteSource: field("source", "Manual"), quantity, unitCode: item.trackingUnitCode, reason: field("reason"),
    }), t.wasteSaved, refreshStock);
  }

  const locationOptions = (list: readonly StockLocation[]) => list.map((location) => <option key={location.id} value={location.id}>{location.name}</option>);

  return <div className="stock">
    {notice && <div role={notice.tone === "error" ? "alert" : "status"} className={`stock__notice stock__notice--${notice.tone}`}>{notice.text}</div>}

    <PanelView title={t.criticalHeading} panel={critical} onRetry={reloadCritical}>
      {(report) => report.items.length === 0 ? <Empty /> : <table>
        <thead><tr><th>{t.name}</th><th>{t.location}</th><th>{t.onHand}</th><th>{t.available}</th><th>{t.threshold}</th><th>{t.status}</th></tr></thead>
        <tbody>{report.items.map((row) => <tr key={`${row.stockItemId}-${row.locationName}`}>
          <td>{row.stockItemName}</td><td>{row.locationName}</td><td>{stat(row.onHandQuantity)} {row.trackingUnitCode}</td>
          <td>{stat(row.availableQuantity)} {row.trackingUnitCode}</td><td>{stat(row.criticalThreshold)}</td>
          <td className={row.isCritical ? "stock__flag stock__flag--critical" : "stock__flag"}>{row.isCritical ? t.critical : t.ok}</td></tr>)}</tbody>
      </table>}
    </PanelView>

    <PanelView title={t.varianceHeading} panel={variance} onRetry={reloadVariance}>
      {(report) => <div>
        <form className="stock__toolbar" onSubmit={(event) => { event.preventDefault(); setRange({ from, to }); }}>
          <TextField id="stock-from" label={t.from} type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
          <TextField id="stock-to" label={t.to} type="date" value={to} onChange={(event) => setTo(event.target.value)} />
          <Button type="submit" variant="secondary">{t.run}</Button>
        </form>
        {report.items.length === 0 ? <Empty /> : <table>
          <thead><tr><th>{t.name}</th><th>{t.location}</th><th>{t.actual}</th><th>{t.theoretical}</th><th>{t.variance}</th><th>{t.variancePercent}</th></tr></thead>
          <tbody>{report.items.map((row) => <tr key={`${row.stockItemId}-${row.locationName}`}>
            <td>{row.stockItemName}</td><td>{row.locationName}</td><td>{stat(row.actualUsage)} {row.trackingUnitCode}</td>
            <td>{stat(row.theoreticalUsage)} {row.trackingUnitCode}</td><td>{stat(row.varianceQuantity)} {row.trackingUnitCode}</td>
            <td>{row.variancePercentage === null ? "—" : `%${stat(row.variancePercentage)}`}</td></tr>)}</tbody>
        </table>}
        {report.excludedForMissingCountsCount > 0 && <p className="stock__hint">{t.excluded}: {report.excludedForMissingCountsCount}</p>}
      </div>}
    </PanelView>

    {canManage && <PanelView title={t.itemsHeading} panel={items} onRetry={reloadItems}>
      {(list) => <div>
        {list.length === 0 ? <Empty /> : <table>
          <thead><tr><th>{t.code}</th><th>{t.name}</th><th>{t.type}</th><th>{t.unit}</th><th>{t.reorderPoint}</th><th>{t.actionsColumn}</th></tr></thead>
          <tbody>{list.map((item) => <tr key={item.id}>
            <td>{item.code}</td><td>{item.name}{item.isActive ? "" : ` (${t.passive})`}</td><td>{itemTypeLabel(item.itemType)}</td><td>{item.trackingUnitCode}</td>
            <td>{item.reorderPoint === null ? t.none : stat(item.reorderPoint)}</td>
            <td className="stock__actions">
              <Button variant="quiet" onClick={() => openAction({ kind: "count", item })}>{t.count}</Button>
              <Button variant="quiet" onClick={() => openAction({ kind: "waste", item })}>{t.waste}</Button>
              <Button variant="quiet" onClick={() => openAction({ kind: "reorder", item })}>{t.setReorder}</Button>
            </td></tr>)}</tbody>
        </table>}

        {action && <form className="stock__form" aria-label={`${action.item.name}`} onSubmit={submitAction}>
          <h4>{action.item.name} · {action.kind === "count" ? t.count : action.kind === "waste" ? t.waste : t.setReorder}</h4>
          {action.kind === "reorder"
            ? <TextField id="stock-reorder" label={t.reorderPoint} hint={t.reorderHint} inputMode="decimal" value={field("reorder")} onChange={set("reorder")} />
            : <>
              <SelectField id="stock-location" label={t.location} value={field("location")} onChange={set("location")}>
                <option value="">—</option>
                {locations.state === "ready" && locationOptions(locations.data)}
              </SelectField>
              <TextField id="stock-quantity" label={action.kind === "count" ? t.countedQuantity : `${t.quantity} (${action.item.trackingUnitCode})`} inputMode="decimal" value={field("quantity")} onChange={set("quantity")} />
              {action.kind === "count"
                ? <TextField id="stock-notes" label={t.notes} value={field("notes")} onChange={set("notes")} />
                : <>
                  <SelectField id="stock-source" label={t.wasteSource} value={field("source", "Manual")} onChange={set("source")}>
                    {wasteSources.map((source) => <option key={source} value={source}>{wasteSourceLabel(source)}</option>)}
                  </SelectField>
                  <TextField id="stock-reason" label={t.reason} value={field("reason")} onChange={set("reason")} />
                </>}
            </>}
          <div className="stock__actions">
            <Button type="submit" disabled={busy}>{t.save}</Button>
            <Button type="button" variant="secondary" onClick={() => setAction(undefined)}>{t.cancel}</Button>
          </div>
        </form>}

        <form className="stock__form" aria-label={t.newItem} onSubmit={(event) => {
          event.preventDefault();
          const reorder = field("n-reorder").trim() === "" ? null : parseQuantity(field("n-reorder"));
          if (field("n-reorder").trim() !== "" && reorder === null) return invalid();
          void act(() => api.createItem({
            code: field("n-code"), name: field("n-name"), itemType: field("n-type", "RawMaterial"), trackingUnitCode: field("n-unit"),
            defaultLocationId: field("n-location") || null, reorderPoint: reorder,
          }), t.itemAdded, refreshStock);
        }}>
          <h4>{t.newItem}</h4>
          <TextField id="n-code" label={t.code} value={field("n-code")} onChange={set("n-code")} />
          <TextField id="n-name" label={t.name} value={field("n-name")} onChange={set("n-name")} />
          <SelectField id="n-type" label={t.type} value={field("n-type", "RawMaterial")} onChange={set("n-type")}>
            {itemTypes.map((type) => <option key={type} value={type}>{itemTypeLabel(type)}</option>)}
          </SelectField>
          <TextField id="n-unit" label={t.unit} value={field("n-unit")} onChange={set("n-unit")} />
          <SelectField id="n-location" label={t.defaultLocation} value={field("n-location")} onChange={set("n-location")}>
            <option value="">{t.none}</option>
            {locations.state === "ready" && locationOptions(locations.data)}
          </SelectField>
          <TextField id="n-reorder" label={t.reorderPoint} inputMode="decimal" value={field("n-reorder")} onChange={set("n-reorder")} />
          <Button type="submit" disabled={busy}>{t.add}</Button>
        </form>
      </div>}
    </PanelView>}

    {canManage && <PanelView title={t.locationsHeading} panel={locations} onRetry={reloadLocations}>
      {(list) => <div>
        {list.length === 0 ? <><Empty /><p className="stock__hint">{t.noLocations}</p></> : <table>
          <thead><tr><th>{t.code}</th><th>{t.name}</th><th>{t.type}</th><th>{t.status}</th></tr></thead>
          <tbody>{list.map((location) => <tr key={location.id}>
            <td>{location.code}</td><td>{location.name}</td><td>{locationTypeLabel(location.locationType)}</td><td>{location.isActive ? t.active : t.passive}</td></tr>)}</tbody>
        </table>}
        <form className="stock__form" aria-label={t.newLocation} onSubmit={(event) => {
          event.preventDefault();
          void act(() => api.createLocation({ code: field("l-code"), name: field("l-name"), locationType: field("l-type", "Warehouse") }), t.locationAdded, reloadLocations);
        }}>
          <h4>{t.newLocation}</h4>
          <TextField id="l-code" label={t.code} value={field("l-code")} onChange={set("l-code")} />
          <TextField id="l-name" label={t.name} value={field("l-name")} onChange={set("l-name")} />
          <SelectField id="l-type" label={t.type} value={field("l-type", "Warehouse")} onChange={set("l-type")}>
            {locationTypes.map((type) => <option key={type} value={type}>{locationTypeLabel(type)}</option>)}
          </SelectField>
          <Button type="submit" disabled={busy}>{t.add}</Button>
        </form>
      </div>}
    </PanelView>}
  </div>;
}
