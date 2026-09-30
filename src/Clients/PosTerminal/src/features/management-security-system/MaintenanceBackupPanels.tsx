import { useCallback, useMemo, useState } from "react";
import { Button } from "../../design-system";
import { managementText } from "../../strings";
import { createSecurityClient, SecurityApiError, type SecurityClient } from "./api";
import { dataClassLabel, formatDuration, formatWhen, jobStatusLabel, type MaintenanceJob } from "./models";
import { Notice, PanelView, usePanel } from "./panel";

const t = managementText.security;

export function MaintenancePanel({ client }: { client?: SecurityClient }) {
  const api = useMemo(() => client ?? createSecurityClient(), [client]);
  const [jobs, reload] = usePanel<readonly MaintenanceJob[]>(useCallback(() => api.listJobs(), [api]), t.loadFailed);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();

  async function run(job: MaintenanceJob) {
    setBusy(true);
    try {
      const status = await api.runJob(job.name);
      setNotice({ tone: "success", text: `${t.maintenance.ran}: ${jobStatusLabel(status.lastStatus)}.` });
      reload();
    } catch (reason) {
      setNotice({ tone: "error", text: reason instanceof SecurityApiError ? reason.message : t.maintenance.failed });
    } finally {
      setBusy(false);
    }
  }

  return <PanelView title={t.maintenance.heading} loading={t.loading} failed={t.loadFailed} panel={jobs} onRetry={reload}>
    {(list) => <div>
      <Notice notice={notice} />
      {list.length === 0 ? <p className="msys__empty">{t.empty}</p> : list.map((job) => <div key={job.name} className="msys__job">
        <strong>{job.description}</strong>
        <span>{jobStatusLabel(job.lastStatus)} · {t.maintenance.lastRun}: {formatWhen(job.lastRunAt)} · {t.maintenance.every} {formatDuration(job.intervalSeconds)}</span>
        {job.lastSummary && <span>{job.lastSummary}</span>}
        {!job.enabled && <span>{t.maintenance.disabled}{job.disabledReason ? `: ${job.disabledReason}` : ""}</span>}
        <div><Button variant="secondary" disabled={busy || !job.enabled} onClick={() => void run(job)}>{t.maintenance.run}</Button></div>
      </div>)}
    </div>}
  </PanelView>;
}

export function BackupPanel({ client }: { client?: SecurityClient }) {
  const api = useMemo(() => client ?? createSecurityClient(), [client]);
  const [data, reload] = usePanel(useCallback(() => Promise.all([api.backupRpo(), api.restoreAttempts()]).then(([rpo, attempts]) => ({ rpo, attempts })), [api]), t.loadFailed);
  return <PanelView title={t.backup.heading} loading={t.loading} failed={t.loadFailed} panel={data} onRetry={reload}>
    {({ rpo, attempts }) => <div>
      <table>
        <thead><tr><th>{t.backup.dataClass}</th><th>{t.backup.target}</th><th>{t.backup.gap}</th><th>{t.backup.status}</th></tr></thead>
        <tbody>{rpo.map((row) => <tr key={row.dataClass}>
          <td>{dataClassLabel(row.dataClass)}</td><td>{formatDuration(row.targetSeconds)}</td>
          <td>{row.measuredGapSeconds === null ? t.backup.noReceipt : formatDuration(row.measuredGapSeconds)}</td>
          <td>{row.meetsTarget ? t.backup.meets : t.backup.misses}</td></tr>)}</tbody>
      </table>
      <h4>{t.backup.attempts}</h4>
      {attempts.length === 0 ? <p className="msys__empty">{t.backup.noAttempts}</p> : <table>
        <thead><tr><th>{t.backup.startedAt}</th><th>{t.backup.dataClass}</th><th>{t.backup.status}</th><th>{t.backup.checks}</th></tr></thead>
        <tbody>{attempts.map((attempt) => <tr key={attempt.artifactId + attempt.startedAtUtc}>
          <td>{formatWhen(attempt.startedAtUtc)}</td><td>{dataClassLabel(attempt.dataClass)}</td>
          <td>{attempt.succeeded ? t.backup.succeeded : t.backup.failedAttempt}</td>
          <td>{attempt.integrityChecksPassed}/{attempt.integrityChecksTotal}</td></tr>)}</tbody>
      </table>}
    </div>}
  </PanelView>;
}
