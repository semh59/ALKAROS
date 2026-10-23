import { useCallback, useMemo, useState } from "react";
import { Button } from "../../design-system";
import { managementText } from "../../strings";
import { createSecurityClient, type SecurityClient } from "./api";
import { fill, formatWhen, type TerminalBinding } from "./models";
import { Notice, PanelView, usePanel } from "../management/panel";
import { ManagementApiError } from "../management/http";

const t = managementText.security.terminals;
const shortId = (id: string) => id.slice(0, 8);

export function TerminalsPanel({ client }: { client?: SecurityClient }) {
  const api = useMemo(() => client ?? createSecurityClient(), [client]);
  const [terminals, reload] = usePanel<readonly TerminalBinding[]>(useCallback(() => api.listTerminals(), [api]), managementText.security.loadFailed);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();

  async function reset(terminal: TerminalBinding) {
    setBusy(true);
    try {
      await api.resetTerminal(terminal.terminalId);
      setNotice({ tone: "success", text: t.resetDone });
      reload();
    } catch (reason) {
      setNotice({ tone: "error", text: reason instanceof ManagementApiError ? reason.message : t.failed });
    } finally {
      setBusy(false);
    }
  }

  return <PanelView title={t.heading} loading={managementText.security.loading} failed={managementText.security.loadFailed} panel={terminals} onRetry={reload}>
    {(list) => <div>
      <p className="msys__hint">{t.hint}</p>
      <Notice notice={notice} />
      {list.length === 0 ? <p className="msys__empty">{t.empty}</p> : <table>
        <thead><tr><th>{t.terminal}</th><th>{t.lastUser}</th><th>{t.lastLogin}</th><th>{t.state}</th><th /></tr></thead>
        <tbody>{list.map((terminal) => <tr key={terminal.terminalId}>
          <td>{shortId(terminal.terminalId)}</td>
          <td>{terminal.lastUserName ?? "—"}</td>
          <td>{formatWhen(terminal.lastLoginAt)}</td>
          <td>{terminal.bound ? t.bound : t.open}</td>
          <td><Button data-telemetry-id="b.management-security-system.terminalspanel.1" variant="secondary" disabled={busy || !terminal.bound}
            aria-label={fill(t.resetLabel, shortId(terminal.terminalId))} onClick={() => void reset(terminal)}>{t.reset}</Button></td>
        </tr>)}</tbody>
      </table>}
    </div>}
  </PanelView>;
}
