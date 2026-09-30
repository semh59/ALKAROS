import { useCallback, useMemo, useState } from "react";
import { Button } from "../../design-system";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { createSettingsClient, type SettingsClient } from "./api";
import { formatReason, formatSettingValue, formatWhen, settingLabel, type SettingRecord } from "./models";
import { PanelView, usePanel } from "../management/panel";
import "./management-settings-history.css";

const t = managementText.settingsHistory;

function History({ client, setting }: { client: SettingsClient; setting: SettingRecord }) {
  const [entries, reload] = usePanel(useCallback(
    () => client.history(setting.key).then((list) => [...list].sort((a, b) => b.changedAt.localeCompare(a.changedAt))), [client, setting.key]), t.loadFailed);
  return <PanelView title={`${t.historyHeading}: ${settingLabel(setting.key)}`} loading={t.loading} failed={t.loadFailed} panel={entries} onRetry={reload}>
    {(list) => list.length === 0 ? <p className="msh__empty">{t.noHistory}</p> : <table>
      <thead><tr><th>{t.when}</th><th>{t.who}</th><th>{t.oldValue}</th><th>{t.newValue}</th><th>{t.reason}</th></tr></thead>
      <tbody>{list.map((entry) => <tr key={entry.settingHistoryId}>
        <td>{formatWhen(entry.changedAt)}</td>
        <td>{entry.changedBy ? `${t.userCode} ${entry.changedBy.slice(0, 8)}` : t.system}</td>
        <td>{formatSettingValue(setting.dataType, entry.oldValue)}</td>
        <td>{formatSettingValue(setting.dataType, entry.newValue)}</td>
        <td>{formatReason(entry.reason)}</td></tr>)}</tbody>
    </table>}
  </PanelView>;
}

export function SettingsHistorySection({ client }: Partial<ManagementSectionProps> & { client?: SettingsClient }) {
  const api = useMemo(() => client ?? createSettingsClient(), [client]);
  const [settings, reload] = usePanel<readonly SettingRecord[]>(useCallback(() => api.listSettings(), [api]), t.loadFailed);
  const [selected, setSelected] = useState<SettingRecord>();

  return <div className="msh">
    <PanelView title={t.heading} loading={t.loading} failed={t.loadFailed} panel={settings} onRetry={reload}>
      {(list) => list.length === 0 ? <p className="msh__empty">{t.empty}</p> : <table>
        <thead><tr><th>{t.setting}</th><th>{t.currentValue}</th><th>{t.updatedAt}</th><th>{t.notes}</th><th>{t.actions}</th></tr></thead>
        <tbody>{list.map((setting) => <tr key={setting.settingId}>
          <td>{settingLabel(setting.key)}</td>
          <td>{formatSettingValue(setting.dataType, setting.value)}</td>
          <td>{formatWhen(setting.updatedAt)}</td>
          <td>{[setting.active ? null : t.inactive, setting.requiresRestart ? t.requiresRestart : null].filter(Boolean).join(" · ") || "—"}</td>
          <td><Button variant="quiet" onClick={() => setSelected(setting)}>{t.showHistory}</Button></td></tr>)}</tbody>
      </table>}
    </PanelView>
    {selected && <History client={api} setting={selected} />}
  </div>;
}
