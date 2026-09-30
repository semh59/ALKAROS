import { useCallback, useMemo, useState, type FormEvent } from "react";
import { Button, TextField } from "../../design-system";
import { managementText } from "../../strings";
import { createSecurityClient, type SecurityClient } from "./api";
import { fill, formatWhen, type CloseSettledResult, type DiagnosticBundle, type OrderBacklog } from "./models";
import { Notice, PanelView, usePanel } from "../management/panel";
import { ManagementApiError } from "../management/http";

const t = managementText.security;
const asIso = (local: string) => new Date(local).toISOString();

export function DiagnosticPanel({ client }: { client?: SecurityClient }) {
  const api = useMemo(() => client ?? createSecurityClient(), [client]);
  const [fields, setFields] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [bundle, setBundle] = useState<DiagnosticBundle>();
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();
  const field = (key: string) => fields[key] ?? "";
  const set = (key: string) => (event: { target: { value: string } }) => setFields((current) => ({ ...current, [key]: event.target.value }));

  async function submit(event: FormEvent) {
    event.preventDefault();
    const correlationIds = field("ids").split(/[\s,;]+/).filter(Boolean);
    if (correlationIds.length === 0 || !field("from") || !field("to") || !field("reason").trim()) {
      return setNotice({ tone: "error", text: t.diagnostic.invalid });
    }
    setBusy(true);
    setBundle(undefined);
    try {
      setBundle(await api.diagnosticBundle({ correlationIds, windowStart: asIso(field("from")), windowEnd: asIso(field("to")), reason: field("reason").trim() }));
      setNotice({ tone: "success", text: t.diagnostic.created });
    } catch (reason) {
      setNotice({ tone: "error", text: reason instanceof ManagementApiError ? reason.message : t.diagnostic.failed });
    } finally {
      setBusy(false);
    }
  }

  function download(result: DiagnosticBundle) {
    const url = URL.createObjectURL(new Blob([JSON.stringify(result, null, 2)], { type: "application/json" }));
    const link = Object.assign(document.createElement("a"), { href: url, download: `tanilama-paketi-${result.bundleId}.json` });
    link.click();
    URL.revokeObjectURL(url);
  }

  return <section className="management__panel" aria-label={t.diagnostic.heading}>
    <h3>{t.diagnostic.heading}</h3>
    <Notice notice={notice} />
    <form className="msys__form" aria-label={t.diagnostic.heading} onSubmit={(event) => void submit(event)}>
      <TextField id="dg-ids" label={t.diagnostic.correlationIds} hint={t.diagnostic.correlationHint} value={field("ids")} onChange={set("ids")} />
      <TextField id="dg-from" label={t.diagnostic.from} type="datetime-local" value={field("from")} onChange={set("from")} />
      <TextField id="dg-to" label={t.diagnostic.to} type="datetime-local" value={field("to")} onChange={set("to")} />
      <TextField id="dg-reason" label={t.diagnostic.reason} value={field("reason")} onChange={set("reason")} />
      <Button type="submit" disabled={busy}>{t.diagnostic.create}</Button>
    </form>
    {bundle && <div>
      <p>{fill(t.diagnostic.summary, bundle.logEntries.length, bundle.systemStatus.unhealthyCheckCount, Math.ceil(bundle.sizeBytes / 1024))}</p>
      <Button variant="secondary" onClick={() => download(bundle)}>{t.diagnostic.download}</Button>
    </div>}
  </section>;
}

export function BacklogPanel({ client }: { client?: SecurityClient }) {
  const api = useMemo(() => client ?? createSecurityClient(), [client]);
  const [backlog, reload] = usePanel<OrderBacklog>(useCallback(() => api.orderBacklog(), [api]), t.loadFailed);
  const [busy, setBusy] = useState(false);
  const [preview, setPreview] = useState<CloseSettledResult>();
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();

  async function run(work: () => Promise<void>) {
    setBusy(true);
    try {
      await work();
    } catch (reason) {
      setNotice({ tone: "error", text: reason instanceof ManagementApiError ? reason.message : t.backlog.failed });
    } finally {
      setBusy(false);
    }
  }

  return <PanelView title={t.backlog.heading} loading={t.loading} failed={t.loadFailed} panel={backlog} onRetry={reload}>
    {(data) => <div>
      <Notice notice={notice} />
      <dl className="msys__facts">
        <div><dt>{t.backlog.live}</dt><dd>{data.liveOrders}</dd></div>
        <div><dt>{t.backlog.settled}</dt><dd>{data.provablySettled}</dd></div>
        <div><dt>{t.backlog.withoutBill}</dt><dd>{data.withoutBill}</dd></div>
        <div><dt>{t.backlog.withOpenBill}</dt><dd>{data.withOpenBill}</dd></div>
        <div><dt>{t.backlog.oldest}</dt><dd>{formatWhen(data.oldestSettledCreatedAt)}</dd></div>
      </dl>
      <div className="msys__actions">
        <Button variant="secondary" disabled={busy} onClick={() => void run(async () => {
          setNotice(undefined);
          setPreview(await api.closeSettled(true));
        })}>{t.backlog.preview}</Button>
      </div>
      {preview && (preview.eligible === 0 ? <p className="msys__hint">{t.backlog.nothing}</p> : <div>
        <p>{fill(t.backlog.eligible, preview.eligible)}{preview.sampleOrderNumbers.length > 0 ? ` (${preview.sampleOrderNumbers.join(", ")})` : ""}</p>
        <Button disabled={busy} onClick={() => void run(async () => {
          const result = await api.closeSettled(false);
          setPreview(undefined);
          setNotice({ tone: result.failed > 0 ? "error" : "success", text: fill(t.backlog.closed, result.closed, result.failed) });
          reload();
        })}>{fill(t.backlog.confirm, preview.eligible)}</Button>
      </div>)}
    </div>}
  </PanelView>;
}
