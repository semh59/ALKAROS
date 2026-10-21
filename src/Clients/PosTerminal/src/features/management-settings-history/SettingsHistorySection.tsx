import { useCallback, useMemo, useState } from "react";
import { Button } from "../../design-system";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { createSettingsClient, type SettingsClient } from "./api";
import { editRule, formatReason, formatSettingValue, formatWhen, settingLabel, type SettingRecord } from "./models";
import { Notice, PanelView, usePanel } from "../management/panel";
import { SettingEditor } from "./SettingEditor";
import "./management-settings-history.css";

const t = managementText.settingsHistory;

function History({ client, setting, revision }: { client: SettingsClient; setting: SettingRecord; revision: number }) {
  const [entries, reload] = usePanel(useCallback(
    () => client.history(setting.key).then((list) => [...list].sort((a, b) => b.changedAt.localeCompare(a.changedAt))), [client, setting.key]), t.loadFailed, revision);
  return <PanelView title={`${t.historyHeading}: ${settingLabel(setting.key)}`} loading={t.loading} failed={t.loadFailed} panel={entries} onRetry={reload}>
    {(list) => list.length === 0 ? <p className="msh__empty">{t.noHistory}</p> : <table>
      <thead><tr><th>{t.when}</th><th>{t.who}</th><th>{t.oldValue}</th><th>{t.newValue}</th><th>{t.reason}</th></tr></thead>
      <tbody>{list.map((entry) => <tr key={entry.settingHistoryId}>
        <td>{formatWhen(entry.changedAt)}</td>
        <td>{entry.changedBy ? entry.changedByName ?? t.unknownPerson : t.system}</td>
        <td>{formatSettingValue(setting.dataType, entry.oldValue, setting.key)}</td>
        <td>{formatSettingValue(setting.dataType, entry.newValue, setting.key)}</td>
        <td>{formatReason(entry.reason)}</td></tr>)}</tbody>
    </table>}
  </PanelView>;
}

export function SettingsHistorySection({ client }: Partial<ManagementSectionProps> & { client?: SettingsClient }) {
  const api = useMemo(() => client ?? createSettingsClient(), [client]);
  const [settings, reload] = usePanel<readonly SettingRecord[]>(useCallback(() => api.listSettings(), [api]), t.loadFailed);
  const [selected, setSelected] = useState<SettingRecord>();
  const [editing, setEditing] = useState<SettingRecord>();
  const [revision, setRevision] = useState(0);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();

  return <div className="msh">
    <Notice notice={notice} />
    <PanelView title={t.heading} loading={t.loading} failed={t.loadFailed} panel={settings} onRetry={reload}>
      {(list) => list.length === 0 ? <p className="msh__empty">{t.empty}</p> : <table>
        <thead><tr><th>{t.setting}</th><th>{t.currentValue}</th><th>{t.updatedAt}</th><th>{t.notes}</th><th>{t.actions}</th></tr></thead>
        <tbody>{list.map((setting) => <tr key={setting.settingId}>
          <td>{settingLabel(setting.key)}</td>
          <td>{formatSettingValue(setting.dataType, setting.value, setting.key)}</td>
          <td>{formatWhen(setting.updatedAt)}</td>
          <td>{[setting.active ? null : t.inactive, setting.requiresRestart ? t.requiresRestart : null].filter(Boolean).join(" · ") || "—"}</td>
          <td className="msh__actions">
            {editRule(setting) && <Button data-telemetry-id="b.management-settings-history.settingshistorysection.2" variant="secondary"
              onClick={() => { setNotice(undefined); setEditing(setting); }}>{t.edit}</Button>}
            <Button data-telemetry-id="b.management-settings-history.settingshistorysection.1" variant="quiet" onClick={() => setSelected(setting)}>{t.showHistory}</Button>
          </td></tr>)}</tbody>
      </table>}
    </PanelView>
    {editing && <SettingEditor key={`${editing.key}-${editing.rowVersion}`} client={api} setting={editing}
      onCancel={() => setEditing(undefined)}
      onSaved={() => { setEditing(undefined); setNotice({ tone: "success", text: t.saved }); setRevision((n) => n + 1); reload(); }}
      onConflict={() => { setEditing(undefined); setNotice({ tone: "error", text: t.conflict }); setRevision((n) => n + 1); reload(); }} />}
    {selected && <History key={selected.key} client={api} setting={selected} revision={revision} />}
  </div>;
}
