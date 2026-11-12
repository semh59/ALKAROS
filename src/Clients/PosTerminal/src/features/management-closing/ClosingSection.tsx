import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { Button, SelectField, StateMessage, TextField } from "../../design-system";
import { formatMoney, formatShortDateTime, businessDate } from "../../format";
import { commonActions, managementText } from "../../strings";
import { createClosingClient, type ClosingClient } from "./closingApi";
import {
  caseStatusLabel, caseStatuses, caseTypeLabel, confirmationStatusLabel, dayStatusLabel, nextStatuses, severityLabel,
  systemActorId, tenderLabel, transitionActionLabel, caseActionLabel, caseActionText, type CaseAction, type BusinessDay, type BusinessDayReport, type CaseStatus, type ClosePreflight, type ManualConfirmation,
  type ReconciliationCase, type SettlementReport,
} from "./models";
import { errorText } from "../management/panel";
import "./management-closing.css";

const t = managementText.closing;
const stamp = (iso: string | null) => (iso ? formatShortDateTime(iso) : "—");
const localToday = () => businessDate();

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
    {panel.state === "error" && <StateMessage tone="error" title={t.loadFailed} actions={<Button data-telemetry-id="b.management-closing.closingsection.1" variant="secondary" onClick={onRetry}>{commonActions.retry}</Button>}><p>{panel.message}</p></StateMessage>}
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

  const dayOpen = day.state === "ready" && day.data?.status === "Open";
  const [preflight, reloadPreflight] = usePanel<ClosePreflight | null>(useCallback(
    () => (canClose && dayOpen ? api.getClosePreflight(date) : Promise.resolve(null)), [api, date, canClose, dayOpen]));
  const [acknowledged, setAcknowledged] = useState(false);
  useEffect(() => setAcknowledged(false), [date, preflight]);
  const [caseNote, setCaseNote] = useState("");
  const [history, setHistory] = useState<{ caseId: string; rows: readonly CaseAction[] }>();
  const [busy, setBusy] = useState(false);

  async function act(work: () => Promise<void>, success: string, refresh: () => void, afterFailure?: () => void) {
    setBusy(true);
    try {
      await work();
      setNotice({ tone: "success", text: success });
      refresh();
    } catch (reason) {
      setNotice({ tone: "error", text: errorText(reason, t.actionFailed) });
      afterFailure?.();
    } finally {
      setBusy(false);
    }
  }
  async function openHistory(caseId: string) {
    setNotice(undefined);
    try {
      setHistory({ caseId, rows: await api.listCaseActions(caseId) });
    } catch (reason) {
      setNotice({ tone: "error", text: errorText(reason, t.actionFailed) });
    }
  }
  function addNote(caseId: string) {
    if (!caseNote.trim()) return setNotice({ tone: "error", text: t.caseNoteRequired });
    void act(() => api.addCaseNote(caseId, caseNote), t.caseNoteAdded, () => {
      setCaseNote("");
      if (history?.caseId === caseId) void openHistory(caseId);
    });
  }
  const refreshDay = () => { reloadDay(); reloadReport(); reloadSettlement(); reloadPreflight(); };
  const dropReportError = report.state === "error" ? { state: "ready", data: null } as const : report;
  const needsAck = preflight.state === "ready" && preflight.data?.requiresAcknowledgement === true;
  const closeBlocked = busy || preflight.state !== "ready" || preflight.data === null || (needsAck && !acknowledged);

  return <div className="closing">
    <form className="closing__date" onSubmit={(event) => { event.preventDefault(); setNotice(undefined); refreshDay(); }}>
      <TextField id="closing-date" label={t.dateLabel} type="date" value={date} onChange={(event) => setDate(event.target.value)} />
      <Button data-telemetry-id="b.management-closing.closingsection.2" type="submit" variant="secondary">{t.load}</Button>
    </form>
    {notice && <div role={notice.tone === "error" ? "alert" : "status"} className={`closing__notice closing__notice--${notice.tone}`}>{notice.text}</div>}

    <PanelView title={t.dayHeading} panel={day} onRetry={reloadDay}>
      {(current: BusinessDay | null) => current === null
        ? <div><p>{t.noDay}</p>{canClose && <Button data-telemetry-id="b.management-closing.closingsection.3" disabled={busy} onClick={() => void act(() => api.openDay(date), t.dayOpened, refreshDay)}>{t.openDay}</Button>}</div>
        : <div>
          <dl className="closing__facts">
            <div><dt>{t.status}</dt><dd>{dayStatusLabel(current.status)}</dd></div>
            <div><dt>{t.openedAt}</dt><dd>{stamp(current.openedAt)}</dd></div>
            <div><dt>{t.closedAt}</dt><dd>{stamp(current.closedAt)}</dd></div>
            <div><dt>{t.revenue}</dt><dd>{formatMoney(current.totalRevenue)}</dd></div>
            {current.status === "Closed" && <div><dt>{t.tips}</dt><dd>{formatMoney(current.totalTipsAmount)}</dd></div>}
            <div><dt>{t.orders}</dt><dd>{current.totalOrdersCount}</dd></div>
            <div><dt>{t.cancelled}</dt><dd>{current.totalCancelledItemsCount}</dd></div>
            <div><dt>{t.kitchenless}</dt><dd>{current.kitchenlessAddedCount}</dd></div>
            <div><dt>{t.printFailed}</dt><dd>{current.totalPrintFailuresCount}</dd></div>
            {current.status === "Closed" && <>
              <div><dt>{t.openCashSessions}</dt><dd>{current.openCashSessionsCount}</dd></div>
              <div><dt>{t.openBills}</dt><dd>{current.openBillsCount}</dd></div>
              <div><dt>{t.unsettledPayments}</dt><dd>{current.unsettledPaymentsCount}</dd></div>
              <div><dt>{t.acknowledgedAtClose}</dt><dd>{current.openItemsAcknowledged ? t.yes : t.no}</dd></div>
            </>}
          </dl>
          {canClose && current.status === "Open" && <form className="closing__close" onSubmit={(event) => {
            event.preventDefault();
            if (closeBlocked) return;
            void act(() => api.closeDay(date, acknowledged), t.dayClosed, refreshDay, reloadPreflight);
          }}>
            <p>{t.closeHint}</p>
            {preflight.state === "loading" && <p aria-busy="true">{t.preflightLoading}</p>}
            {preflight.state === "error" && <StateMessage tone="error" title={t.preflightFailed} actions={<Button data-telemetry-id="b.management-closing.closingsection.10" variant="secondary" onClick={reloadPreflight}>{commonActions.retry}</Button>}><p>{preflight.message}</p></StateMessage>}
            {preflight.state === "ready" && preflight.data && <>
              <dl className="closing__facts" aria-label={t.preflightHeading}>
                <div><dt>{t.revenue}</dt><dd>{formatMoney(preflight.data.revenue)}</dd></div>
                <div><dt>{t.tips}</dt><dd>{formatMoney(preflight.data.tips)}</dd></div>
                <div><dt>{t.orders}</dt><dd>{preflight.data.orders}</dd></div>
                <div><dt>{t.cancelled}</dt><dd>{preflight.data.cancelledItems}</dd></div>
                <div><dt>{t.kitchenless}</dt><dd>{preflight.data.kitchenlessAddedCount}</dd></div>
                <div><dt>{t.printFailed}</dt><dd>{preflight.data.printFailures}</dd></div>
                <div><dt>{t.openCashSessions}</dt><dd>{preflight.data.openCashSessions}</dd></div>
                <div><dt>{t.openBills}</dt><dd>{preflight.data.openBills}</dd></div>
                <div><dt>{t.unsettledPayments}</dt><dd>{preflight.data.unsettledPayments}</dd></div>
              </dl>
              {needsAck && <div className="closing__warning" role="alert">
                <strong>{t.openItemsTitle}</strong>
                <p>{t.openItemsBody}</p>
                <label className="closing__check"><input type="checkbox" checked={acknowledged} onChange={(event) => setAcknowledged(event.target.checked)} />{t.openItemsAck}</label>
              </div>}
            </>}
            <Button data-telemetry-id="b.management-closing.closingsection.4" type="submit" disabled={closeBlocked}>{t.closeDay}</Button>
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

    {closedDay && <PanelView title={t.tableHeading} panel={dropReportError} onRetry={reloadReport}>
      {(data) => !data || data.businessDay.tableBreakdown.length === 0 ? <Empty /> : <table>
        <thead><tr><th>{t.table}</th><th>{t.tableOrders}</th><th>{t.sales}</th></tr></thead>
        <tbody>{data.businessDay.tableBreakdown.map((row) => <tr key={row.tableId}><td>{row.tableNumber ?? t.tableRemoved}</td><td>{row.orders}</td><td>{formatMoney(row.sales)}</td></tr>)}</tbody>
      </table>}
    </PanelView>}

    {closedDay && <PanelView title={t.cashReconciliationHeading} panel={dropReportError} onRetry={reloadReport}>
      {(data) => !data || !data.businessDay.cashReconciliation ? <Empty /> : <dl className="closing__facts">
        <div><dt>{t.sessionsClosed}</dt><dd>{data.businessDay.cashReconciliation.sessionsClosed}</dd></div>
        <div><dt>{t.expected}</dt><dd>{formatMoney(data.businessDay.cashReconciliation.expected)}</dd></div>
        <div><dt>{t.actual}</dt><dd>{formatMoney(data.businessDay.cashReconciliation.counted)}</dd></div>
        <div><dt>{t.difference}</dt><dd>{formatMoney(data.businessDay.cashReconciliation.difference)}</dd></div>
        <div><dt>{t.managerApprovedCloses}</dt><dd>{data.businessDay.cashReconciliation.managerApprovedCloses}</dd></div>
        <div><dt>{t.selfReconciled}</dt><dd>{data.businessDay.cashReconciliation.selfReconciled}</dd></div>
      </dl>}
    </PanelView>}

    {closedDay && <PanelView title={t.printHeading} panel={dropReportError} onRetry={reloadReport}>
      {(data) => !data || data.printSummaries.length === 0 ? <Empty /> : <table>
        <thead><tr><th>{t.station}</th><th>{t.printTotal}</th><th>{t.printFailedJobs}</th><th>{t.printRecovered}</th></tr></thead>
        <tbody>{data.printSummaries.map((row) => <tr key={row.stationName}><td>{row.stationName}</td><td>{row.totalPrintJobs}</td><td>{row.failedPrintJobs}</td><td>{row.recoveredPrintJobs}</td></tr>)}</tbody>
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
          {canManage && <Button data-telemetry-id="b.management-closing.closingsection.5" variant="secondary" disabled={busy} onClick={() => void act(() => api.scanPayments(), t.scanDone, reloadCases)}>{t.scan}</Button>}
        </div>
        {canManage && <TextField id="closing-case-note" label={t.caseNote} value={caseNote} onChange={(event) => setCaseNote(event.target.value)} />}
        {rows.length === 0 ? <Empty /> : <table>
          <thead><tr><th>{t.caseType}</th><th>{t.severity}</th><th>{t.status}</th><th>{t.amount}</th><th>{t.opened}</th><th>{t.actions}</th></tr></thead>
          <tbody>{rows.map((row) => <tr key={row.caseId}>
            <td>{caseTypeLabel(row.caseType)}</td><td>{severityLabel(row.severity)}</td><td>{caseStatusLabel(row.status)}</td>
            <td>{formatMoney(row.discrepancyAmount)}</td><td>{stamp(row.openedAt)}</td>
            <td className="closing__actions">
              <Button data-telemetry-id="b.management-closing.closingsection.6" variant="quiet" disabled={busy} onClick={() => void openHistory(row.caseId)}>{t.caseHistory}</Button>
              {canManage && <Button data-telemetry-id="b.management-closing.closingsection.7" variant="quiet" disabled={busy} onClick={() => addNote(row.caseId)}>{t.caseAddNote}</Button>}
              {canManage && nextStatuses(row.status, row.caseType).map((target) =>
              <Button data-telemetry-id="b.management-closing.closingsection.8" key={target} variant="quiet" disabled={busy}
                onClick={() => void act(() => api.transitionCase(row.caseId, target, row.rowVersion, caseNote), t.caseUpdated, () => { setCaseNote(""); reloadCases(); })}>
                {transitionActionLabel(target)}
              </Button>)}</td>
          </tr>)}</tbody>
        </table>}
        {history && <div className="closing__history" role="region" aria-label={t.caseHistoryHeading}>
          <h4>{t.caseHistoryHeading}</h4>
          {history.rows.length === 0 ? <p className="closing__empty">{t.caseHistoryEmpty}</p> : <table>
            <thead><tr><th>{t.caseActionType}</th><th>{t.caseActionWhen}</th><th>{t.caseActionBy}</th><th>{t.caseActionDetail}</th></tr></thead>
            <tbody>{history.rows.map((action) => <tr key={action.actionId}>
              <td>{caseActionLabel(action.actionType)}</td><td>{stamp(action.performedAt)}</td><td>{action.performedByName ?? (action.performedBy === systemActorId ? t.caseActionSystem : t.caseActionUnknownUser)}</td><td>{caseActionText(action.detailsJson)}</td></tr>)}</tbody>
          </table>}
          <Button data-telemetry-id="b.management-closing.closingsection.9" variant="secondary" onClick={() => setHistory(undefined)}>{t.caseHistoryClose}</Button>
        </div>}
      </div>}
    </PanelView>
  </div>;
}
