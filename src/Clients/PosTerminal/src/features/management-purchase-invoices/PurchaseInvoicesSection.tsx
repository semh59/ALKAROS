import { useCallback, useMemo, useState, type ChangeEvent } from "react";
import { Button, SelectField, TextField } from "../../design-system";
import { formatMoney, formatQuantity } from "../../format";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { Notice, PanelView, errorText, usePanel } from "../management/panel";
import { createPurchaseInvoiceClient, type PurchaseInvoiceClient } from "./api";
import {
  invoiceStatusLabel, invoiceStatuses, isReturn, parseQuantity, type LookupItem, type LookupLocation, type PurchaseInvoice, type PurchaseInvoiceSummary,
} from "./models";
import "./management-purchase-invoices.css";

const t = managementText.purchaseInvoices;

export function PurchaseInvoicesSection({ client }: Partial<ManagementSectionProps> & { client?: PurchaseInvoiceClient }) {
  const api = useMemo(() => client ?? createPurchaseInvoiceClient(), [client]);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();
  const [busy, setBusy] = useState(false);
  const [statusFilter, setStatusFilter] = useState("Draft");
  const [refreshKey, setRefreshKey] = useState(0);
  const [open, setOpen] = useState<PurchaseInvoice>();
  const [picks, setPicks] = useState<Record<string, { stock: string; factor: string }>>({});
  const [locationId, setLocationId] = useState("");
  const [qnbSummary, setQnbSummary] = useState<string>();

  const [invoices, reload] = usePanel<readonly PurchaseInvoiceSummary[]>(useCallback(() => api.list(statusFilter), [api, statusFilter]), t.loadFailed, refreshKey);
  const [lookups] = usePanel<{ locations: readonly LookupLocation[]; items: readonly LookupItem[] }>(useCallback(
    () => Promise.all([api.listLocations(), api.listStockItems()]).then(([locations, items]) => ({ locations, items })), [api]), t.lookupFailed);

  async function act(work: () => Promise<void>, success: string) {
    setBusy(true);
    try {
      await work();
      setNotice({ tone: "success", text: success });
    } catch (reason) {
      setNotice({ tone: "error", text: errorText(reason, t.actionFailed) });
    } finally {
      setBusy(false);
    }
  }

  const itemName = (id: string | null) => id === null || lookups.state !== "ready" ? "—" : lookups.data.items.find((item) => item.id === id)?.name ?? "—";
  const unmapped = open?.lines.filter((line) => line.stockItemId === null).length ?? 0;
  const isDraft = open?.status === "Draft";

  const upload = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    void act(async () => {
      const created = await api.importXml(await file.text());
      setOpen(created);
      setPicks({});
      setRefreshKey((key) => key + 1);
    }, t.imported);
  };

  const fetchFromQnb = () => void act(async () => {
    const result = await api.fetchFromQnb();
    setQnbSummary(t.fetchSummary(result));
    setRefreshKey((key) => key + 1);
  }, t.fetched);

  const mapLine = (lineId: string) => {
    const pick = picks[lineId];
    const factor = parseQuantity(pick?.factor ?? "1");
    if (!open || !pick?.stock) return setNotice({ tone: "error", text: t.pickItem });
    if (factor === null || factor <= 0) return setNotice({ tone: "error", text: t.invalidFactor });
    void act(async () => {
      setOpen(await api.mapLine(open.invoiceId, lineId, pick.stock, factor));
      setRefreshKey((key) => key + 1);
    }, t.mapped);
  };

  const finish = (work: () => Promise<void>, success: string) => void act(async () => {
    await work();
    setOpen(await api.get(open!.invoiceId));
    setRefreshKey((key) => key + 1);
  }, success);

  return <div className="mpi">
    <div className="mpi__bar">
      <div>
        <label htmlFor="mpi-file">{t.upload}</label>
        <input id="mpi-file" type="file" accept=".xml,text/xml,application/xml" disabled={busy} onChange={upload} />
      </div>
      <Button variant="secondary" disabled={busy} onClick={fetchFromQnb}>{t.fetchQnb}</Button>
      <SelectField id="mpi-status" label={t.status} value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
        <option value="">{t.allStatuses}</option>
        {invoiceStatuses.map((status) => <option key={status} value={status}>{invoiceStatusLabel(status)}</option>)}
      </SelectField>
    </div>
    <Notice notice={notice} />
    {qnbSummary && <p className="mpi__hint">{qnbSummary}</p>}
    <PanelView title={t.listHeading} loading={t.loading} failed={t.loadFailed} panel={invoices} onRetry={reload}>
      {(rows) => rows.length === 0 ? <p className="mpi__hint">{t.empty}</p> : <table>
        <thead><tr>
          <th>{t.invoiceNumber}</th><th>{t.issueDate}</th><th>{t.supplier}</th><th>{t.status}</th>
          <th className="mpi__num">{t.lines}</th><th className="mpi__num">{t.unmappedLines}</th><th className="mpi__num">{t.netTotal}</th><th>{t.action}</th></tr></thead>
        <tbody>{rows.map((row) => <tr key={row.invoiceId}>
          <td>{row.invoiceNumber}{isReturn(row.kind) && ` (${t.returnBadge})`}</td><td>{new Date(`${row.issueDate}T00:00:00`).toLocaleDateString("tr-TR")}</td><td>{row.supplierName}</td>
          <td>{invoiceStatusLabel(row.status)}</td>
          <td className="mpi__num">{row.lineCount}</td><td className="mpi__num">{row.unmappedLineCount}</td><td className="mpi__num">{formatMoney(row.netTotal)}</td>
          <td><Button variant="secondary" disabled={busy} onClick={() => void act(async () => { setOpen(await api.get(row.invoiceId)); setPicks({}); }, t.opened)}>{t.open}</Button></td>
        </tr>)}</tbody>
      </table>}
    </PanelView>
    {open && <section aria-label={t.detailHeading}>
      <h3>{isReturn(open.kind) ? t.returnHeading : t.detailHeading}: {open.invoiceNumber} — {open.supplierName} ({invoiceStatusLabel(open.status)})</h3>
      {open.supplierId === null && isDraft && <p role="alert" className="mpi__warning">{t.noSupplier}</p>}
      <table>
        <thead><tr>
          <th>{t.lineNo}</th><th>{t.description}</th><th className="mpi__num">{t.quantity}</th><th className="mpi__num">{t.unitPrice}</th>
          <th className="mpi__num">{t.lineNet}</th><th>{t.stockItem}</th></tr></thead>
        <tbody>{open.lines.map((line) => <tr key={line.lineId}>
          <td>{line.lineNumber}</td><td>{line.description}</td>
          <td className="mpi__num">{formatQuantity(line.quantity)} {line.unitCode}</td>
          <td className="mpi__num">{formatMoney(line.unitPrice)}</td><td className="mpi__num">{formatMoney(line.lineNet)}</td>
          <td>{line.stockItemId !== null ? `${itemName(line.stockItemId)} (× ${formatQuantity(line.conversionFactor ?? 1)})` : isDraft && lookups.state === "ready"
            ? <div className="mpi__map">
              <SelectField id={`mpi-stock-${line.lineId}`} label={t.stockItem} value={picks[line.lineId]?.stock ?? ""}
                onChange={(event) => setPicks({ ...picks, [line.lineId]: { stock: event.target.value, factor: picks[line.lineId]?.factor ?? "1" } })}>
                <option value="">{t.choose}</option>
                {lookups.data.items.map((item) => <option key={item.id} value={item.id}>{item.name} ({item.trackingUnitCode})</option>)}
              </SelectField>
              <TextField id={`mpi-factor-${line.lineId}`} label={t.factor} value={picks[line.lineId]?.factor ?? "1"}
                onChange={(event) => setPicks({ ...picks, [line.lineId]: { stock: picks[line.lineId]?.stock ?? "", factor: event.target.value } })} />
              <Button variant="secondary" disabled={busy} onClick={() => mapLine(line.lineId)}>{t.map}</Button>
            </div>
            : t.notMapped}</td>
        </tr>)}</tbody>
      </table>
      {isDraft && <>
        <p className="mpi__hint">{t.factorHint}</p>
        {unmapped > 0 && <p className="mpi__warning">{t.unmappedWarning(unmapped)}</p>}
        {lookups.state === "ready" && <div className="mpi__actions">
          <SelectField id="mpi-location" label={isReturn(open.kind) ? t.locationReturn : t.location} value={locationId} onChange={(event) => setLocationId(event.target.value)}>
            <option value="">{t.choose}</option>
            {lookups.data.locations.map((location) => <option key={location.id} value={location.id}>{location.name}</option>)}
          </SelectField>
          <Button disabled={busy || unmapped > 0 || locationId === ""} onClick={() => finish(() => api.approve(open.invoiceId, locationId), isReturn(open.kind) ? t.approvedReturn : t.approved)}>{isReturn(open.kind) ? t.approveReturn : t.approve}</Button>
          <Button variant="secondary" disabled={busy} onClick={() => finish(() => api.reject(open.invoiceId), t.rejected)}>{t.reject}</Button>
        </div>}
      </>}
    </section>}
  </div>;
}
