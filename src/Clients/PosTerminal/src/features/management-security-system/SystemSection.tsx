import { useCallback, useMemo, useState, type FormEvent } from "react";
import { Button, TextField } from "../../design-system";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { createSystemClient, SecurityApiError, type SystemClient } from "./api";
import { alertSeverityLabel, alertStatusLabel, formatWhen, healthStatusLabel, type Alert, type AlertAction } from "./models";
import { Notice, PanelView, usePanel } from "./panel";
import "./management-security-system.css";

const t = managementText.system;
const actions: readonly { action: AlertAction; label: string }[] = [
  { action: "acknowledge", label: t.acknowledge },
  { action: "escalate", label: t.escalate },
  { action: "suppress", label: t.suppress },
  { action: "resolve", label: t.resolve },
];

export function SystemSection({ capabilities, client }: ManagementSectionProps & { client?: SystemClient }) {
  const api = useMemo(() => client ?? createSystemClient(), [client]);
  const canManage = capabilities.has("observability.manage");
  const [alerts, reloadAlerts] = usePanel<readonly Alert[]>(useCallback(() => api.activeAlerts(), [api]), t.loadFailed);
  const [checks, reloadChecks] = usePanel(useCallback(() => api.unhealthyChecks(), [api]), t.loadFailed);
  const [pending, setPending] = useState<{ alert: Alert; action: AlertAction }>();
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!pending) return;
    if (pending.action === "resolve" && !reason.trim()) return setNotice({ tone: "error", text: t.reasonRequired });
    setBusy(true);
    try {
      await api.actOnAlert(pending.alert, pending.action, reason.trim());
      setNotice({ tone: "success", text: t.done });
      setPending(undefined);
      reloadAlerts();
    } catch (failure) {
      setNotice({ tone: "error", text: failure instanceof SecurityApiError ? failure.message : t.actionFailed });
    } finally {
      setBusy(false);
    }
  }

  return <div className="msys">
    <Notice notice={notice} />
    <PanelView title={t.alertsHeading} loading={t.loading} failed={t.loadFailed} panel={alerts} onRetry={reloadAlerts}>
      {(list) => <div>
        {list.length === 0 ? <p className="msys__empty">{t.noAlerts}</p> : list.map((alert) => <div key={alert.alertId} className="msys__job">
          <strong>{alert.title}</strong>
          <span>{alertSeverityLabel(alert.severity)} · {alertStatusLabel(alert.status)} · {formatWhen(alert.openedAt)}</span>
          <span>{alert.message}</span>
          {canManage && <div className="msys__actions">{actions.map(({ action, label }) =>
            <Button key={action} variant="quiet" disabled={busy} onClick={() => { setNotice(undefined); setReason(""); setPending({ alert, action }); }}>{label}</Button>)}
          </div>}
        </div>)}
        {pending && <form className="msys__form" aria-label={pending.alert.title} onSubmit={(event) => void submit(event)}>
          <TextField id="al-reason" label={pending.action === "resolve" ? t.resolutionReason : t.reasonOptional} value={reason} onChange={(event) => setReason(event.target.value)} />
          <div className="msys__actions">
            <Button type="submit" disabled={busy}>{t.confirm}</Button>
            <Button type="button" variant="secondary" onClick={() => setPending(undefined)}>{t.cancel}</Button>
          </div>
        </form>}
      </div>}
    </PanelView>
    <PanelView title={t.healthHeading} loading={t.loading} failed={t.loadFailed} panel={checks} onRetry={reloadChecks}>
      {(list) => list.length === 0 ? <p className="msys__empty">{t.allHealthy}</p> : <table>
        <thead><tr><th>{t.target}</th><th>{t.status}</th><th>{t.checkedAt}</th></tr></thead>
        <tbody>{list.map((check) => <tr key={check.healthCheckId}>
          <td>{check.target}</td><td>{healthStatusLabel(check.status)}</td><td>{formatWhen(check.checkedAt)}</td></tr>)}</tbody>
      </table>}
    </PanelView>
  </div>;
}
