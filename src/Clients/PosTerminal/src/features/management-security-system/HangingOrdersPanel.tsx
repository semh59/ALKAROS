import { useCallback, useMemo, useState } from "react";
import { Button, SelectField, TextField } from "../../design-system";
import { cancelReasonText, managementText } from "../../strings";
import { createSecurityClient, type SecurityClient } from "./api";
import { fill, formatWhen, type ClearHangingResult, type HangingOrder } from "./models";
import { Notice, PanelView, usePanel } from "../management/panel";
import { ManagementApiError } from "../management/http";

const t = managementText.security;
const h = t.hanging;

const ageText = (createdAt: string) => {
  const hours = Math.floor((Date.now() - Date.parse(createdAt)) / 3_600_000);
  return hours < 1 ? h.ageUnderHour : fill(h.age, hours);
};

export function HangingOrdersPanel({ client }: { client?: SecurityClient }) {
  const api = useMemo(() => client ?? createSecurityClient(), [client]);
  const [orders, reload] = usePanel<readonly HangingOrder[]>(useCallback(() => api.hangingOrders(), [api]), t.loadFailed);
  const [busy, setBusy] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [preview, setPreview] = useState<readonly HangingOrder[]>();
  const [choice, setChoice] = useState("");
  const [reason, setReason] = useState("");
  const [results, setResults] = useState<ClearHangingResult["results"]>([]);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();

  async function run(work: () => Promise<void>) {
    setBusy(true);
    try {
      await work();
    } catch (failure) {
      setNotice({ tone: "error", text: failure instanceof ManagementApiError ? failure.message : h.failed });
    } finally {
      setBusy(false);
    }
  }

  return <PanelView title={h.heading} loading={t.loading} failed={t.loadFailed} panel={orders} onRetry={reload}>
    {(list) => <div>
      <Notice notice={notice} />
      <p className="msys__hint">{h.hint}</p>
      {list.length === 0 && results.length === 0 && <p className="msys__hint">{h.empty}</p>}
      {list.length > 0 && <ul className="msys__hanging">
        {list.map((order) => <li key={order.orderId}>
          <strong>{order.orderNumber}</strong> · {h.statuses[order.status] ?? h.unknownStatus} · {order.tableNumber ? `${h.table} ${order.tableNumber}` : "—"} · {formatWhen(order.createdAt)} ({ageText(order.createdAt)})
          <span> · {order.stockLines} {h.stock} · {order.wasteLines} {h.waste} · {order.servedLines} {h.served}</span>
        </li>)}
      </ul>}
      {results.length > 0 && <ul className="msys__hanging">
        {results.map((row) => <li key={row.orderId}><strong>{row.orderNumber ?? "—"}</strong> · {h.outcomes[row.outcome] ?? h.unknownOutcome}</li>)}
      </ul>}
      {list.length > 0 && <div className="msys__actions">
        <Button data-telemetry-id="b.management-security-system.hangingorderspanel.1" variant="secondary" disabled={busy} onClick={() => void run(async () => {
          setNotice(undefined);
          setResults([]);
          setConfirming(false);
          setPreview((await api.clearHanging(true, "", [])).orders);
        })}>{h.preview}</Button>
      </div>}
      {preview && preview.length > 0 && <div>
        <SelectField label={h.reason} value={choice} disabled={busy} onChange={(event) => {
          setChoice(event.target.value);
          setConfirming(false);
          setReason(event.target.value === cancelReasonText.other ? "" : event.target.value);
        }}>
          <option value="">{cancelReasonText.placeholder}</option>
          {cancelReasonText.presets.map((preset) => <option key={preset} value={preset}>{preset}</option>)}
          <option value={cancelReasonText.other}>{cancelReasonText.other}</option>
        </SelectField>
        {choice === cancelReasonText.other && <TextField label={cancelReasonText.write} maxLength={500} value={reason} onChange={(event) => { setReason(event.target.value); setConfirming(false); }} />}
        {confirming
          ? <div className="msys__actions">
            <span role="alert">{h.confirmQuestion}</span>
            <Button data-telemetry-id="b.management-security-system.hangingorderspanel.2" disabled={busy} onClick={() => void run(async () => {
              const result = await api.clearHanging(false, reason.trim(), preview.map((order) => order.orderId));
              const cleared = result.results.filter((row) => row.outcome === "Cleared").length;
              const failed = result.results.length - cleared;
              setResults(result.results);
              setPreview(undefined);
              setChoice("");
              setReason("");
              setConfirming(false);
              setNotice({ tone: failed === 0 ? "success" : "error", text: failed === 0 ? fill(h.done, cleared, failed) : `${fill(h.done, cleared, failed)} ${h.doneHint}` });
              reload();
            })}>{fill(h.confirmYes, preview.length)}</Button>
            <Button data-telemetry-id="b.management-security-system.hangingorderspanel.3" variant="secondary" disabled={busy} onClick={() => setConfirming(false)}>{h.dismiss}</Button>
          </div>
          : <Button data-telemetry-id="b.management-security-system.hangingorderspanel.4" disabled={busy || !reason.trim()} onClick={() => setConfirming(true)}>{fill(h.confirm, preview.length)}</Button>}
      </div>}
    </div>}
  </PanelView>;
}
