import { useCallback, useMemo, useState } from "react";
import { Button, SelectField } from "../../design-system";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { Notice, PanelView, errorText, usePanel } from "../management/panel";
import { createErrorEventsClient, type ErrorEventsClient } from "./api";
import {
  categoryLabel, fill, formatWhen, layerLabel, severityLabel, statusLabel, type ErrorEventDetail, type ErrorFilter,
} from "./models";
import "./management-error-events.css";

const t = managementText.errors;
const layers = ["Database", "Api", "Background", "Printer", "Payment", "Kitchen", "Integration", "Browser", "Security", "Backup"];
const severities = ["Info", "Warning", "Error", "Critical"];
const statuses = ["Open", "Acknowledged", "Resolved"];

function Filter({ id, label, value, options, map, onChange }: {
  id: string; label: string; value: string; options: readonly string[]; map: (value: string) => string; onChange: (value: string) => void;
}) {
  return <SelectField id={id} label={label} value={value} onChange={(event) => onChange(event.target.value)}>
    <option value="">{t.all}</option>
    {options.map((option) => <option key={option} value={option}>{map(option)}</option>)}
  </SelectField>;
}

export function ErrorEventsSection({ capabilities, client }: ManagementSectionProps & { client?: ErrorEventsClient }) {
  const api = useMemo(() => client ?? createErrorEventsClient(), [client]);
  const canManage = capabilities.has("observability.manage");
  const [filter, setFilter] = useState<ErrorFilter>({ layer: "", severity: "", status: "" });
  const [page, setPage] = useState(1);
  const [refresh, setRefresh] = useState(0);
  const [list, reload] = usePanel(useCallback(() => api.list(filter, page), [api, filter, page]), t.loadFailed, refresh);
  const [channel] = usePanel(useCallback(() => api.channelConfigured(), [api]), t.loadFailed);
  const [detail, setDetail] = useState<ErrorEventDetail>();
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();

  const change = (key: keyof ErrorFilter) => (value: string) => { setFilter({ ...filter, [key]: value }); setPage(1); };

  async function act(fingerprint: string, action: "acknowledge" | "resolve") {
    setBusy(true);
    setNotice(undefined);
    try {
      await api.change(fingerprint, action);
      setNotice({ tone: "success", text: t.done });
      setDetail(undefined);
      setRefresh((value) => value + 1);
    } catch (failure) {
      setNotice({ tone: "error", text: errorText(failure, t.actionFailed) });
    } finally {
      setBusy(false);
    }
  }

  async function open(fingerprint: string) {
    try {
      setDetail(await api.detail(fingerprint));
    } catch (failure) {
      setNotice({ tone: "error", text: errorText(failure, t.loadFailed) });
    }
  }

  return <div className="merr">
    <Notice notice={notice} />
    {channel.state === "ready" && <p className="merr__hint">{channel.data ? t.channelOn : t.channelOff}</p>}
    <div className="merr__filters">
      <Filter id="er-layer" label={t.filterLayer} value={filter.layer} options={layers} map={layerLabel} onChange={change("layer")} />
      <Filter id="er-severity" label={t.filterSeverity} value={filter.severity} options={severities} map={severityLabel} onChange={change("severity")} />
      <Filter id="er-status" label={t.filterStatus} value={filter.status} options={statuses} map={statusLabel} onChange={change("status")} />
    </div>
    <PanelView title={t.heading} loading={t.loading} failed={t.loadFailed} panel={list} onRetry={reload}>
      {(data) => data.items.length === 0 ? <p className="merr__empty">{t.empty}</p> : <>
        <table>
          <thead><tr>
            <th>{t.category}</th><th>{t.filterLayer}</th><th>{t.filterSeverity}</th><th>{t.filterStatus}</th>
            <th>{t.count}</th><th>{t.lastSeen}</th><th>{t.terminal}</th><th>{t.action}</th>
          </tr></thead>
          <tbody>{data.items.map((item) => <tr key={item.fingerprint}>
            <td>{categoryLabel(item.category)}{item.synthetic ? ` (${t.test})` : ""}</td>
            <td>{layerLabel(item.layer)}</td><td>{severityLabel(item.severity)}</td><td>{statusLabel(item.status)}</td>
            <td>{item.occurrenceCount}</td><td>{formatWhen(item.lastSeenAt)}</td><td>{item.terminalLabel ?? "—"}</td>
            <td><div className="merr__actions">
              <Button data-telemetry-id="b.management-error-events.erroreventssection.1" variant="quiet" onClick={() => void open(item.fingerprint)}>{t.details}</Button>
              {canManage && item.status === "Open" && <Button data-telemetry-id="b.management-error-events.erroreventssection.2" variant="quiet" disabled={busy} onClick={() => void act(item.fingerprint, "acknowledge")}>{t.acknowledge}</Button>}
              {canManage && item.status !== "Resolved" && <Button data-telemetry-id="b.management-error-events.erroreventssection.3" variant="quiet" disabled={busy} onClick={() => void act(item.fingerprint, "resolve")}>{t.resolve}</Button>}
            </div></td>
          </tr>)}</tbody>
        </table>
        <div className="merr__actions">
          <Button data-telemetry-id="b.management-error-events.erroreventssection.4" variant="secondary" disabled={page <= 1} onClick={() => setPage(page - 1)}>{t.previous}</Button>
          <span>{fill(t.pageOf, page, Math.max(1, Math.ceil(data.total / data.pageSize)))}</span>
          <Button data-telemetry-id="b.management-error-events.erroreventssection.5" variant="secondary" disabled={page * data.pageSize >= data.total} onClick={() => setPage(page + 1)}>{t.next}</Button>
        </div>
      </>}
    </PanelView>
    {detail && <section className="management__panel" aria-label={t.detailHeading}>
      <h3>{t.detailHeading}</h3>
      <ul>{detail.recent.map((row) => <li key={row.occurredAt}>{formatWhen(row.occurredAt)} · {severityLabel(row.severity)} · {row.occurrences}</li>)}</ul>
    </section>}
  </div>;
}
