import { useCallback, useEffect, useState } from "react";
import { Button, StateMessage, TextField } from "../../design-system";
import { refundMethodLabels, refundStatusLabels, refundText } from "../../strings";
import { describeRefundFailure, type BillTenderSummary, type BillingRefundClient, type RefundRequestRow } from "./refundApi";

export interface RefundPanelProps {
  client: BillingRefundClient;
  canCancel: boolean;
  canRequestRefund: boolean;
  canApproveRefund: boolean;
}

type Action = "cancel" | "request" | "approve" | "reject" | "complete";

const money = (value: number) => new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" }).format(value);
const isOpen = (row: RefundRequestRow) => row.status === "Requested" || row.status === "AwaitingManualRefund";

export function RefundPanel({ client, canCancel, canRequestRefund, canApproveRefund }: RefundPanelProps) {
  const [tenders, setTenders] = useState<BillTenderSummary | null>(null);
  const [requests, setRequests] = useState<readonly RefundRequestRow[]>([]);
  const [loadError, setLoadError] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const [cancelled, setCancelled] = useState(false);
  const [cancelReason, setCancelReason] = useState("");
  const [texts, setTexts] = useState<Record<string, string>>({});
  const setText = (key: string, value: string) => setTexts((current) => ({ ...current, [key]: value }));

  const load = useCallback(async () => {
    try {
      const [summary, rows] = await Promise.all([
        client.getTenders(),
        canRequestRefund || canApproveRefund ? client.listRequests().catch(() => []) : Promise.resolve([]),
      ]);
      setTenders(summary);
      setRequests(rows);
      setLoadError(false);
    } catch {
      setLoadError(true);
    }
  }, [client, canRequestRefund, canApproveRefund]);

  useEffect(() => { void load(); }, [load]);

  async function run(action: Action, call: () => Promise<RefundRequestRow | void>) {
    setBusy(true);
    setError(undefined);
    setNotice(undefined);
    try {
      const result = await call();
      if (action === "cancel" || (result && result.billCancelled)) {
        setCancelled(true);
        return;
      }
      setNotice({
        request: refundText.requested, approve: refundText.approved, reject: refundText.rejected, complete: refundText.completed,
      }[action as Exclude<Action, "cancel">]);
      await load();
    } catch (reason) {
      setError(describeRefundFailure(reason));
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

  const refundable = canRequestRefund
    ? tenders.allocations.filter((line) => line.paymentStatus === "Approved" && !requests.some((row) => row.paymentId === line.paymentId && isOpen(row)))
    : [];
  const showCancel = canCancel && tenders.allocations.length === 0;
  if (!showCancel && refundable.length === 0 && requests.length === 0 && !notice && !error) return null;

  return <section className="refund-panel" aria-labelledby="refund-panel-heading">
    <h2 id="refund-panel-heading">{refundText.heading}</h2>
    {error && <StateMessage tone="error" title={error} />}
    {notice && <p role="status">{notice}</p>}

    {refundable.length > 0 && <div className="refund-panel__group">
      <h3>{refundText.refundHeading}</h3>
      {refundable.map((line) => <div key={line.paymentId} className="refund-panel__row">
        <strong>{money(line.amount)}</strong>
        <TextField
          label={refundText.refundReasonLabel}
          maxLength={500}
          value={texts[`reason:${line.paymentId}`] ?? ""}
          onChange={(event) => setText(`reason:${line.paymentId}`, event.target.value)}
        />
        <Button variant="secondary" disabled={busy} onClick={() => void run("request", () => client.requestRefund(line.paymentId, texts[`reason:${line.paymentId}`] ?? ""))}>
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
          <TextField
            label={refundText.rejectNoteLabel}
            maxLength={500}
            value={texts[`note:${row.requestId}`] ?? ""}
            onChange={(event) => setText(`note:${row.requestId}`, event.target.value)}
          />
          <Button disabled={busy} onClick={() => void run("approve", () => client.approve(row.requestId))}>{refundText.approve}</Button>
          <Button variant="secondary" disabled={busy} onClick={() => void run("reject", () => client.reject(row.requestId, texts[`note:${row.requestId}`] ?? ""))}>
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
          <Button disabled={busy} onClick={() => void run("complete", () => client.complete(row.requestId, texts[`slip:${row.requestId}`] ?? ""))}>
            {refundText.complete}
          </Button>
        </>}
      </div>)}
    </div>}

    {showCancel && <div className="refund-panel__group">
      <TextField
        label={refundText.cancelLabel}
        maxLength={500}
        value={cancelReason}
        onChange={(event) => setCancelReason(event.target.value)}
      />
      <Button variant="secondary" disabled={busy} onClick={() => void run("cancel", () => client.cancelBill(cancelReason))}>
        {refundText.cancelAction}
      </Button>
    </div>}
  </section>;
}
