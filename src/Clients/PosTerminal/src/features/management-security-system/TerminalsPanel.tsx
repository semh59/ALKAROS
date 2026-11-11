import { useCallback, useMemo, useState } from "react";
import { Button } from "../../design-system";
import { managementText } from "../../strings";
import { createSecurityClient, type SecurityClient } from "./api";
import { fill, formatWhen, type TerminalBinding } from "./models";
import { Notice, PanelView, usePanel } from "../management/panel";
import { ManagementApiError } from "../management/http";

const t = managementText.security.terminals;
const shortId = (id: string) => id.slice(0, 8);
const label = (terminal: TerminalBinding) => terminal.name ?? shortId(terminal.terminalId);

export function TerminalsPanel({ client }: { client?: SecurityClient }) {
  const api = useMemo(() => client ?? createSecurityClient(), [client]);
  const [terminals, reload] = usePanel<readonly TerminalBinding[]>(useCallback(() => api.listTerminals(), [api]), managementText.security.loadFailed);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();

  async function rename(terminal: TerminalBinding, name: string) {
    setBusy(true);
    try {
      await api.renameTerminal(terminal.terminalId, name);
      setNotice({ tone: "success", text: t.nameSaved });
      reload();
    } catch (reason) {
      setNotice({ tone: "error", text: reason instanceof ManagementApiError ? reason.message : t.failed });
    } finally {
      setBusy(false);
    }
  }

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
        <thead><tr><th>{t.terminal}</th><th>{t.name}</th><th>{t.lastUser}</th><th>{t.lastLogin}</th><th>{t.state}</th><th>{t.reset}</th></tr></thead>
        <tbody>{list.map((terminal) => <tr key={terminal.terminalId}>
          <td>{label(terminal)}</td>
          <td><NameEditor key={`${terminal.terminalId}:${terminal.name ?? ""}`} terminal={terminal} busy={busy} onSave={(name) => void rename(terminal, name)} /></td>
          <td>{terminal.lastUserName ?? "—"}</td>
          <td>{formatWhen(terminal.lastLoginAt)}</td>
          <td>{terminal.bound ? t.bound : t.open}</td>
          <td><Button data-telemetry-id="b.management-security-system.terminalspanel.1" variant="secondary" disabled={busy || !terminal.bound}
            aria-label={fill(t.resetLabel, label(terminal))} onClick={() => void reset(terminal)}>{t.reset}</Button></td>
        </tr>)}</tbody>
      </table>}
    </div>}
  </PanelView>;
}

function NameEditor({ terminal, busy, onSave }: { terminal: TerminalBinding; busy: boolean; onSave: (name: string) => void }) {
  const [draft, setDraft] = useState(terminal.name ?? "");
  const id = `terminal-name-${terminal.terminalId}`;
  return <div className="msys__name">
    <input id={id} value={draft} maxLength={40} placeholder={t.namePlaceholder} aria-label={fill(t.nameLabel, label(terminal))}
      onChange={(event) => setDraft(event.target.value)} />
    <Button data-telemetry-id="b.management-security-system.terminalspanel.2" variant="secondary" disabled={busy || draft.trim() === (terminal.name ?? "")} aria-label={fill(t.nameSaveLabel, label(terminal))} onClick={() => onSave(draft)}>{t.nameSave}</Button>
  </div>;
}
