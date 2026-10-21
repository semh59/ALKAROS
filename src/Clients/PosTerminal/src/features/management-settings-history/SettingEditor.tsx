import { useState, type FormEvent } from "react";
import { Button, SelectField, TextField } from "../../design-system";
import { managementText } from "../../strings";
import { ManagementApiError } from "../management/http";
import type { SettingsClient } from "./api";
import { durationToMinutes, editRule, minutesToDuration, settingLabel, type SettingRecord } from "./models";

const t = managementText.settingsHistory;
const MaxReasonLength = 500;

function initialValue(setting: SettingRecord): string {
  if (setting.dataType === "Duration") return String(durationToMinutes(setting.value));
  return setting.value;
}

export function SettingEditor({ client, setting, onSaved, onConflict, onCancel }: {
  client: SettingsClient; setting: SettingRecord; onSaved: () => void; onConflict: () => void; onCancel: () => void;
}) {
  const rule = editRule(setting);
  const [value, setValue] = useState(() => initialValue(setting));
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string>();
  if (!rule) return null;

  async function submit(event: FormEvent) {
    event.preventDefault();
    let newValue = value;
    if (rule!.kind !== "toggle") {
      const number = Number(value);
      if (value.trim() === "" || !Number.isInteger(number)) return setProblem(t.invalidNumber);
      if (number < rule!.min || number > rule!.max) return setProblem(rule!.hint);
      newValue = rule!.kind === "minutes" ? minutesToDuration(number) : String(number);
    }
    if (newValue === setting.value) return setProblem(t.unchanged);
    if (reason.trim() === "") return setProblem(t.reasonRequired);
    setBusy(true);
    setProblem(undefined);
    try {
      await client.update(setting.key, newValue, setting.rowVersion, reason.trim());
      onSaved();
    } catch (failure) {
      if (failure instanceof ManagementApiError && failure.status === 409) return onConflict();
      setProblem(failure instanceof ManagementApiError ? failure.message : t.saveFailed);
    } finally {
      setBusy(false);
    }
  }

  return <form className="msh__form" aria-label={`${t.editHeading}: ${settingLabel(setting.key)}`} onSubmit={submit}>
    <h4>{`${t.editHeading}: ${settingLabel(setting.key)}`}</h4>
    {rule.kind === "toggle"
      ? <SelectField label={t.newValue} value={value} onChange={(event) => setValue(event.target.value)}>
        <option value="true">{t.yes}</option><option value="false">{t.no}</option>
      </SelectField>
      : <TextField label={rule.kind === "minutes" ? t.newValueMinutes : t.newValue} hint={rule.hint} inputMode="numeric"
        value={value} onChange={(event) => setValue(event.target.value)} />}
    <TextField label={t.reasonLabel} hint={t.reasonHint} maxLength={MaxReasonLength} value={reason} onChange={(event) => setReason(event.target.value)} />
    {problem && <p role="alert" className="msh__problem">{problem}</p>}
    <div className="msh__actions">
      <Button data-telemetry-id="b.management-settings-history.settingeditor.1" type="submit" disabled={busy}>{busy ? t.saving : t.save}</Button>
      <Button data-telemetry-id="b.management-settings-history.settingeditor.2" type="button" variant="quiet" disabled={busy} onClick={onCancel}>{t.cancel}</Button>
    </div>
  </form>;
}
