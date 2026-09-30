import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { Button, SelectField, StateMessage, TextField } from "../../design-system";
import { formatMoney } from "../../format";
import { commonActions, managementText } from "../../strings";
import { createClosingClient, type ClosingClient } from "./closingApi";
import {
  caseStatusLabel, caseStatuses, caseTypeLabel, confirmationStatusLabel, dayStatusLabel, nextStatuses, severityLabel,
  tenderLabel, transitionActionLabel, type BusinessDay, type BusinessDayReport, type CaseStatus, type ManualConfirmation,
  type ReconciliationCase, type SettlementReport,
} from "./models";
import { errorText } from "../management/panel";
import "./management-closing.css";

const t = managementText.closing;
const stamp = (iso: string | null) => (iso ? new Date(iso).toLocaleString("tr-TR", { dateStyle: "short", timeStyle: "short" }) : "—");
const localToday = () => new Date().toLocaleDateString("sv-SE");

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
  return <section className="closing__panel" aria-label={title}>
    <h3>{title}</h3>
    {panel.state === "loading" && <p aria-busy="true">{t.loading}</p>}
    {panel.state === "error" && <StateMessage tone="error" title={t.loadFailed} actions={<Button variant="secondary" onClick={onRetry}>{commonActions.retry}</Button>}><p>{panel.message}</p></StateMessage>}
    {panel.state === "ready" && children(panel.data)}
  </section>;
}

const Empty = () => <p className="closing__empty">{t.empty}</p>;

export function ClosingSection({ capabilities, client }: { capabilities: ReadonlySet<string>; client?: ClosingClient }) {
  const api = useMemo(() => client ?? createClosingClient(), [client]);
  const canClose = capabilities.has("reports.close-day");
  const canManage = capabilities.has("reconciliation.manage");
  const [date, setDate] = useState(localToday);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();

  const [day, reloadDay] = usePanel(useCallback(() => api.getDay(date), [api, date]));
  const closedDay = day.state === "ready" && day.data?.status === "Closed";
  const [report, reloadReport] = usePanel<BusinessDayReport | null>(useCallback(
    () => (closedDay ? api.getFullReport(date) : Promise.resolve(null)), [api, date, closedDay]));
  const [settlement, reloadSettlement] = usePanel(useCallback(() => api.getSettlement(date), [api, date]));
  const [confirmations, reloadConfirmations] = usePanel(useCallback(() => api.listManualConfirmations(), [api]));
  const [caseStatus, setCaseStatus] = useState<CaseStatus>("Open");
  const [cases, reloadCases] = usePanel(useCallback(() => api.listCases(caseStatus), [api, caseStatus]));

  const [cancelledItems, setCancelledItems] = useState("0");
  const [printFailures, setPrintFailures] = useState("0");
  const [caseNote, setCaseNote] = useState("");
  const [busy, setBusy] = useState(false);

  async function act(work: () => Promise<void>, success: string, refresh: () => void) {
    setBusy(true);
    try {
      await work();
      setNotice({ tone: "success", text: success });
      refresh();
    } catch (reason) {
      setNotice({ tone: "error", text: errorText(reason, t.actionFailed) });
    } finally {
      setBusy(false);
    }
  }
  const refreshDay = () => { reloadDay(); reloadReport(); reloadSettlement(); };
  const count = (value: string) => Math.max(0, Math.trunc(Number(value) || 0));

  return <div className="closing">
    <form className="closing__date" onSubmit={(event) => { event.preventDefault(); setNotice(undefined); refreshDay(); }}>
      <TextField id="closing-date" label={t.dateLabel} type="date" value={date} onChange={(event) => setDate(event.target.value)} />
      <Button type="submit" variant="secondary">{t.load}</Button>
    </form>
    {notice && <div role={notice.tone === "error" ? "alert" : "status"} className={`closing__notice closing__notice--${notice.tone}`}>{notice.text}</div>}

    <PanelView title={t.dayHeading} panel={day} onRetry={reloadDay}>
      {(current: BusinessDay | null) => current === null
        ? <div><p>{t.noDay}</p>{canClose && <Button disabled={busy} onClick={() => void act(() => api.openDay(date), t.dayOpened, refreshDay)}>{t.openDay}</Button>}</div>
        : <div>
          <dl className="closing__facts">
            <div><dt>{t.status}</dt><dd>{dayStatusLabel(current.status)}</dd></div>
            <div><dt>{t.openedAt}</dt><dd>{stamp(current.openedAt)}</dd></div>
            <div><dt>{t.closedAt}</dt><dd>{stamp(current.closedAt)}</dd></div>
            <div><dt>{t.revenue}</dt><dd>{formatMoney(current.totalRevenue)}</dd></div>
            <div><dt>{t.orders}</dt><dd>{current.totalOrdersCount}</dd></div>
            <div><dt>{t.cancelled}</dt><dd>{current.totalCancelledItemsCount}</dd></div>
            <div><dt>{t.printFailed}</dt><dd>{current.totalPrintFailuresCount}</dd></div>
          </dl>
          {canClose && current.status === "Open" && <form className="closing__close" onSubmit={(event) => {
            event.preventDefault();
            void act(() => api.closeDay(date, count(cancelledItems), count(printFailures)), t.dayClosed, refreshDay);
          }}>
            <p>{t.closeHint}</p>
            <TextField id="closing-cancelled" label={t.cancelledItems} type="number" min={0} value={cancelledItems} onChange={(event) => setCancelledItems(event.target.value)} />
            <TextField id="closing-print" label={t.printFailures} type="number" min={0} value={printFailures} onChange={(event) => setPrintFailures(event.target.value)} />
            <Button type="submit" disabled={busy}>{t.closeDay}</Button>
          </form>}
        </div>}
    </PanelView>

    {closedDay && <PanelView title={t.waiters} panel={report} onRetry={reloadReport}>
      {(data) => !data || data.waiterSummaries.length === 0 ? <Empty /> : <table>
        <thead><tr><th>{t.waiter}</th><th>{t.served}</th><th>{t.sales}</th><th>{t.cancellations}</th><th>{t.discounts}</th></tr></thead>
        <tbody>{data.waiterSummaries.map((row) => <tr key={row.waiterUserId}>
          <td>{row.waiterUserId.slice(0, 8)}</td><td>{row.ordersServedCount}</td><td>{formatMoney(row.totalSalesAmount)}</td>
          <td>{row.cancellationsCount}</td><td>{formatMoney(row.discountsAppliedAmount)}</td></tr>)}</tbody>
      </table>}
    </PanelView>}

    <PanelView title={t.settlementHeading} panel={settlement} onRetry={reloadSettlement}>
      {(data: SettlementReport) => <div>
        <h4>{t.paymentMix}</h4>
        {data.paymentMix.length === 0 ? <Empty /> : <table>
          <thead><tr><th>{t.method}</th><th>{t.count}</th><th>{t.amount}</th></tr></thead>
          <tbody>{data.paymentMix.map((row) => <tr key={row.method}><td>{tenderLabel(row.method)}</td><td>{row.approvedCount}</td><td>{formatMoney(row.approvedAmount)}</td></tr>)}</tbody>
        </table>}
        <h4>{t.unsettled}</h4>
        <dl className="closing__facts">
          <div><dt>{t.unknown}</dt><dd>{data.unsettledPayments.unknownCount} · {formatMoney(data.unsettledPayments.unknownAmount)}</dd></div>
          <div><dt>{t.reconciliationRequired}</dt><dd>{data.unsettledPayments.reconciliationRequiredCount} · {formatMoney(data.unsettledPayments.reconciliationRequiredAmount)}</dd></div>
        </dl>
        <h4>{t.cashSessions}</h4>
        {data.cashSessions.length === 0 ? <Empty /> : <table>
          <thead><tr><th>{t.status}</th><th>{t.expected}</th><th>{t.actual}</th><th>{t.difference}</th></tr></thead>
          <tbody>{data.cashSessions.map((row) => <tr key={row.cashSessionId}>
            <td>{row.isOpen ? t.sessionOpen : t.sessionClosed}</td><td>{formatMoney(row.expectedCash)}</td><td>{formatMoney(row.actualCash)}</td><td>{formatMoney(row.difference)}</td></tr>)}</tbody>
        </table>}
        <h4>{t.caseTotals}</h4>
        {data.reconciliationTotals.length === 0 ? <Empty /> : <table>
          <thead><tr><th>{t.caseType}</th><th>{t.openCases}</th><th>{t.resolvedCases}</th><th>{t.openDiscrepancy}</th></tr></thead>
          <tbody>{data.reconciliationTotals.map((row) => <tr key={row.caseType}>
            <td>{caseTypeLabel(row.caseType)}</td><td>{row.openCount}</td><td>{row.resolvedCount}</td><td>{formatMoney(row.openDiscrepancyAmount)}</td></tr>)}</tbody>
        </table>}
        <p className="closing__hint">{t.unavailableSections}</p>
      </div>}
    </PanelView>

    <PanelView title={t.manualHeading} panel={confirmations} onRetry={reloadConfirmations}>
      {(rows: readonly ManualConfirmation[]) => rows.length === 0 ? <Empty /> : <table>
        <thead><tr><th>{t.slip}</th><th>{t.amount}</th><th>{t.status}</th><th>{t.requestedAt}</th><th>{t.decidedAt}</th></tr></thead>
        <tbody>{rows.map((row) => <tr key={row.confirmationId}>
          <td>{row.slipNumber}</td><td>{formatMoney(row.amount)}</td><td>{confirmationStatusLabel(row.status)}</td><td>{stamp(row.requestedAt)}</td><td>{stamp(row.decidedAt)}</td></tr>)}</tbody>
      </table>}
    </PanelView>

    <PanelView title={t.casesHeading} panel={cases} onRetry={reloadCases}>
      {(rows: readonly ReconciliationCase[]) => <div>
        <div className="closing__toolbar">
          <SelectField id="closing-case-status" label={t.caseStatusFilter} value={caseStatus} onChange={(event) => setCaseStatus(event.target.value as CaseStatus)}>
            {caseStatuses.map((status) => <option key={status} value={status}>{caseStatusLabel(status)}</option>)}
          </SelectField>
          {canManage && <Button variant="secondary" disabled={busy} onClick={() => void act(() => api.scanPayments(), t.scanDone, reloadCases)}>{t.scan}</Button>}
        </div>
        {canManage && <TextField id="closing-case-note" label={t.caseNote} value={caseNote} onChange={(event) => setCaseNote(event.target.value)} />}
        {rows.length === 0 ? <Empty /> : <table>
          <thead><tr><th>{t.caseType}</th><th>{t.severity}</th><th>{t.status}</th><th>{t.amount}</th><th>{t.opened}</th>{canManage && <th>{t.actions}</th>}</tr></thead>
          <tbody>{rows.map((row) => <tr key={row.caseId}>
            <td>{caseTypeLabel(row.caseType)}</td><td>{severityLabel(row.severity)}</td><td>{caseStatusLabel(row.status)}</td>
            <td>{formatMoney(row.discrepancyAmount)}</td><td>{stamp(row.openedAt)}</td>
            {canManage && <td className="closing__actions">{nextStatuses(row.status, row.caseType).map((target) =>
              <Button key={target} variant="quiet" disabled={busy}
                onClick={() => void act(() => api.transitionCase(row.caseId, target, row.rowVersion, caseNote), t.caseUpdated, reloadCases)}>
                {transitionActionLabel(target)}
              </Button>)}</td>}
          </tr>)}</tbody>
        </table>}
      </div>}
    </PanelView>
  </div>;
}
