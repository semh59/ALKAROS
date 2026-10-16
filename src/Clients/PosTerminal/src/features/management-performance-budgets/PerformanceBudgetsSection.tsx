import { useCallback, useMemo, useState, type FormEvent } from "react";
import { Button, SelectField, TextField } from "../../design-system";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { Notice, PanelView, errorText, usePanel } from "../management/panel";
import { createPerformanceBudgetsClient, type PerformanceBudgetsClient } from "./api";
import { isValidBudget, metricLabel, metrics } from "./models";
import "./management-performance-budgets.css";

const t = managementText.performanceBudgets;

export function PerformanceBudgetsSection({ capabilities, client }: ManagementSectionProps & { client?: PerformanceBudgetsClient }) {
  const api = useMemo(() => client ?? createPerformanceBudgetsClient(), [client]);
  const canManage = capabilities.has("observability.manage");
  const [refresh, setRefresh] = useState(0);
  const [list, reload] = usePanel(useCallback(() => api.list(), [api]), t.loadFailed, refresh);
  const [metric, setMetric] = useState<string>(metrics[0]);
  const [name, setName] = useState("");
  const [limit, setLimit] = useState("");
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();

  async function run(action: () => Promise<void>, success: string) {
    setBusy(true);
    setNotice(undefined);
    try {
      await action();
      setNotice({ tone: "success", text: success });
      setRefresh((value) => value + 1);
    } catch (failure) {
      setNotice({ tone: "error", text: errorText(failure, t.actionFailed) });
    } finally {
      setBusy(false);
    }
  }

  function save(event: FormEvent) {
    event.preventDefault();
    const p95Ms = Number(limit.replace(",", "."));
    if (!isValidBudget(name, p95Ms)) {
      setNotice({ tone: "error", text: t.invalid });
      return;
    }
    void run(async () => {
      await api.save({ metric, name: name.trim(), p95Ms });
      setName("");
      setLimit("");
    }, t.saved);
  }

  return <div className="mbud">
    <Notice notice={notice} />
    <PanelView title={t.heading} loading={t.loading} failed={t.loadFailed} panel={list} onRetry={reload}>
      {(data) => <>
        <p className="mbud__hint">{t.defaultHint.replace("{0}", String(data.defaultRequestP95Ms))}</p>
        {data.items.length === 0 ? <p className="mbud__empty">{t.empty}</p> : <table>
          <thead><tr><th>{t.metric}</th><th>{t.name}</th><th>{t.limit}</th><th>{t.action}</th></tr></thead>
          <tbody>{data.items.map((item) => <tr key={`${item.metric}|${item.name}`}>
            <td>{metricLabel(item.metric)}</td><td>{item.name}</td><td>{item.p95Ms}</td>
            <td>{canManage && <div className="mbud__actions">
              <Button data-telemetry-id="b.management-performance-budgets.performancebudgetssection.1" variant="quiet" disabled={busy}
                onClick={() => { setMetric(item.metric); setName(item.name); setLimit(String(item.p95Ms)); }}>{t.edit}</Button>
              <Button data-telemetry-id="b.management-performance-budgets.performancebudgetssection.2" variant="quiet" disabled={busy}
                onClick={() => void run(() => api.remove(item.metric, item.name), t.removed)}>{t.remove}</Button>
            </div>}</td>
          </tr>)}</tbody>
        </table>}
      </>}
    </PanelView>
    {canManage && <form className="mbud__form" aria-label={t.formLabel} onSubmit={save}>
      <SelectField id="pb-metric" label={t.metric} value={metric} onChange={(event) => setMetric(event.target.value)}>
        {metrics.map((option) => <option key={option} value={option}>{metricLabel(option)}</option>)}
      </SelectField>
      <TextField id="pb-name" label={t.name} value={name} maxLength={200} onChange={(event) => setName(event.target.value)} />
      <TextField id="pb-limit" label={t.limit} value={limit} inputMode="decimal" onChange={(event) => setLimit(event.target.value)} />
      <Button data-telemetry-id="b.management-performance-budgets.performancebudgetssection.3" type="submit" disabled={busy}>{t.save}</Button>
    </form>}
  </div>;
}
