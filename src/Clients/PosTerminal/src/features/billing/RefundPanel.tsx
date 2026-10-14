import { useCallback, useEffect, useRef, useState } from "react";
import { Button, SelectField, StateMessage, TextField } from "../../design-system";
import { refundMethodLabels, refundStatusLabels, refundText } from "../../strings";
import {
  RefundApiError, describeAllowanceFailure, describeRefundFailure,
  type BillTenderSummary, type BillingRefundClient, type RefundAllowance, type RefundRequestRow,
} from "./refundApi";

export interface RefundPanelProps {
  client: BillingRefundClient;
  canCancel: boolean;
  canRequestRefund: boolean;
  canApproveRefund: boolean;
}

type Action = "cancel" | "request" | "approve" | "reject" | "complete" | "allowance";

const money = (value: number) => new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" }).format(value);
const isOpen = (row: RefundRequestRow) => row.status === "Requested" || row.status === "AwaitingManualRefund";
const filled = (value: string | undefined) => (value ?? "").trim().length > 0;
const parseAmount = (value: string | undefined) => {
  const parsed = Number((value ?? "").trim().replace(",", "."));
  return filled(value) && Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
};

export function RefundPanel({ client, canCancel, canRequestRefund, canApproveRefund }: RefundPanelProps) {
  const [tenders, setTenders] = useState<BillTenderSummary | null>(null);
  const [requests, setRequests] = useState<readonly RefundRequestRow[]>([]);
  const [listFailed, setListFailed] = useState(false);
  const [loadError, setLoadError] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const [allowance, setAllowance] = useState<RefundAllowance | null>(null);
  const [cancelled, setCancelled] = useState(false);
  const [confirming, setConfirming] = useState<string | null>(null);
  const [texts, setTexts] = useState<Record<string, string>>({});
  const [choices, setChoices] = useState<Record<string, string>>({});
  const loaded = useRef(false);
  const setText = (key: string, value: string) => setTexts((current) => ({ ...current, [key]: value }));
  const choose = (key: string, value: string) => {
    setChoices((current) => ({ ...current, [key]: value }));
    setText(key, value === refundText.reasonOther ? "" : value);
  };
  // Preset reasons; the "other" choice opens a free text field. The final reason string lives in texts[key].
  const reasonField = (key: string, label: string) => <>
    <SelectField label={label} value={choices[key] ?? ""} disabled={busy} onChange={(event) => choose(key, event.target.value)}>
      <option value="">{refundText.reasonPlaceholder}</option>
      {refundText.reasonPresets.map((preset) => <option key={preset} value={preset}>{preset}</option>)}
      <option value={refundText.reasonOther}>{refundText.reasonOther}</option>
    </SelectField>
    {choices[key] === refundText.reasonOther && <TextField
      label={refundText.reasonWrite}
      maxLength={500}
      value={texts[key] ?? ""}
      onChange={(event) => setText(key, event.target.value)}
    />}
  </>;

  const load = useCallback(async () => {
    try {
      const summary = await client.getTenders();
      let rows: readonly RefundRequestRow[] = [];
      let failed = false;
      if (canRequestRefund || canApproveRefund) {
        try {
          rows = await client.listRequests();
        } catch (reason) {
          // 403: the user simply has no right to the list; anything else means the list is unknown.
          failed = !(reason instanceof RefundApiError && reason.status === 403);
        }
      }
      let shift: RefundAllowance | null = null;
      if (canRequestRefund) {
        // The remaining allowance is a convenience; when it cannot be read the refund flow is unaffected.
        shift = await client.getAllowance().catch(() => null);
      }
      setTenders(summary);
      setAllowance(shift);
      setRequests(rows);
      setListFailed(failed);
      setLoadError(false);
      loaded.current = true;
    } catch (reason) {
      if (loaded.current) setError(describeRefundFailure(reason));
      else setLoadError(true);
    }
  }, [client, canRequestRefund, canApproveRefund]);

  useEffect(() => { void load(); }, [load]);

  async function run(
    action: Action,
    call: () => Promise<RefundRequestRow | RefundAllowance | void>,
    describe: (reason: unknown) => string = describeRefundFailure,
  ) {
    setConfirming(null);
    setBusy(true);
    setError(undefined);
    setNotice(undefined);
    try {
      const result = await call() as RefundRequestRow | void;
      if (action === "cancel" || (result && result.billCancelled)) {
        setCancelled(true);
        return;
      }
      const fromAllowance = action === "request" && result && result.approvedFromAllowance
        ? (result.status === "Completed" ? refundText.completedFromAllowance : refundText.reservedFromAllowance)
        : undefined;
      setNotice(fromAllowance ?? {
        request: refundText.requested, approve: refundText.approved, reject: refundText.rejected,
        complete: refundText.completed, allowance: refundText.allowanceGranted,
      }[action as Exclude<Action, "cancel">]);
      await load();
    } catch (reason) {
      setError(describe(reason));
      if (reason instanceof RefundApiError && reason.status === 409) await load();
    } finally {
      setBusy(false);
    }
  }

  if (cancelled) {
    return <section className="refund-panel"><StateMessage tone="success" title={refundText.cancelled} /></section>;
  }
  if (loadError) {
    return <section className="refund-panel">
      <StateMessage tone="error" title={refundText.loadFailed} actions={<Button variant="secondary" onClick={() => void load()}>{refundText.refresh}</Button>} />
    </section>;
  }
  if (!tenders) return <section className="refund-panel"><p role="status">{refundText.loading}</p></section>;

  const approvedLines = tenders.allocations.filter((line) => line.paymentStatus === "Approved");
  const refundable = canRequestRefund && !listFailed
    ? approvedLines.filter((line) => !requests.some((row) => row.paymentId === line.paymentId && isOpen(row)))
    : [];
  const listBlocksRefund = canRequestRefund && listFailed && approvedLines.length > 0;
  const showCancel = canCancel && tenders.allocations.length === 0 && !tenders.unsettledPayment;
  if (!showCancel && refundable.length === 0 && requests.length === 0 && !notice && !error && !listBlocksRefund) return null;

  // Destructive buttons ask once more instead of acting on the first tap.
  function guarded(key: string, label: string, confirmLabel: string, variant: "primary" | "secondary", disabled: boolean, action: Action, call: () => Promise<RefundRequestRow | void>) {
    if (confirming === key) {
      return <>
        <span role="alert">{refundText.confirmQuestion}</span>
        <Button disabled={busy} onClick={() => void run(action, call)}>{confirmLabel}</Button>
        <Button variant="secondary" onClick={() => setConfirming(null)}>{refundText.dismiss}</Button>
      </>;
    }
    return <Button variant={variant} disabled={busy || disabled} onClick={() => setConfirming(key)}>{label}</Button>;
  }

  return <section className="refund-panel" aria-labelledby="refund-panel-heading">
    <h2 id="refund-panel-heading">{refundText.heading}</h2>
    {error && <StateMessage tone="error" title={error} />}
    {notice && <p role="status">{notice}</p>}
    {canRequestRefund && allowance && allowance.hasOpenSession && allowance.allowance > 0 &&
      <p>{refundText.allowanceRemaining}: <strong>{money(allowance.remaining)}</strong></p>}
    {listBlocksRefund && <StateMessage tone="warning" title={refundText.listFailed} actions={<Button variant="secondary" onClick={() => void load()}>{refundText.refresh}</Button>} />}

    {refundable.length > 0 && <div className="refund-panel__group">
      <h3>{refundText.refundHeading}</h3>
      {refundable.map((line) => <div key={line.paymentId} className="refund-panel__row">
        <strong>{money(line.amount)}</strong>
        {reasonField(`reason:${line.paymentId}`, refundText.refundReasonLabel)}
        <Button variant="secondary" disabled={busy || !filled(texts[`reason:${line.paymentId}`])} onClick={() => void run("request", () => client.requestRefund(line.paymentId, texts[`reason:${line.paymentId}`] ?? ""))}>
          {refundText.refundAction}
        </Button>
      </div>)}
    </div>}

    {requests.length > 0 && <div className="refund-panel__group">
      <h3>{refundText.requestsHeading}</h3>
      {requests.map((row) => <div key={row.requestId} className="refund-panel__row">
        <strong>{money(row.amount)} · {refundMethodLabels[row.method] ?? refundText.unknown}</strong>
        <span className="refund-panel__status">{refundStatusLabels[row.status] ?? refundText.unknown}</span>
        <span>{row.reason}</span>
        {row.status === "Requested" && canApproveRefund && <>
          {row.requestedBy && <>
            <TextField
              label={refundText.allowanceLabel}
              inputMode="decimal"
              value={texts[`allowance:${row.requestId}`] ?? ""}
              onChange={(event) => setText(`allowance:${row.requestId}`, event.target.value)}
            />
            <Button
              variant="secondary"
              disabled={busy || parseAmount(texts[`allowance:${row.requestId}`]) === null}
              onClick={() => void run(
                "allowance",
                () => client.setAllowance(row.requestedBy ?? "", parseAmount(texts[`allowance:${row.requestId}`]) ?? 0),
                describeAllowanceFailure,
              )}
            >
              {refundText.allowanceAction}
            </Button>
          </>}
          {reasonField(`note:${row.requestId}`, refundText.rejectNoteLabel)}
          {guarded(`approve:${row.requestId}`, refundText.approve, refundText.approveConfirm, "primary", false, "approve", () => client.approve(row.requestId))}
          <Button variant="secondary" disabled={busy || !filled(texts[`note:${row.requestId}`])} onClick={() => void run("reject", () => client.reject(row.requestId, texts[`note:${row.requestId}`] ?? ""))}>
            {refundText.reject}
          </Button>
        </>}
        {row.status === "AwaitingManualRefund" && <>
          <TextField
            label={refundText.slipLabel}
            maxLength={50}
            value={texts[`slip:${row.requestId}`] ?? ""}
            onChange={(event) => setText(`slip:${row.requestId}`, event.target.value)}
          />
          <Button disabled={busy || !filled(texts[`slip:${row.requestId}`])} onClick={() => void run("complete", () => client.complete(row.requestId, texts[`slip:${row.requestId}`] ?? ""))}>
            {refundText.complete}
          </Button>
        </>}
      </div>)}
    </div>}

    {showCancel && <div className="refund-panel__group">
      {reasonField("cancel", refundText.cancelLabel)}
      {guarded("cancel", refundText.cancelAction, refundText.cancelConfirm, "secondary", !filled(texts.cancel), "cancel", () => client.cancelBill(texts.cancel ?? ""))}
    </div>}
  </section>;
}
