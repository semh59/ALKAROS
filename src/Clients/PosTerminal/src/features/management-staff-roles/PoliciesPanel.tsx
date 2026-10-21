import { useCallback, useState, type FormEvent } from "react";
import { Button, TextField } from "../../design-system";
import { managementText } from "../../strings";
import type { StaffClient } from "./api";
import { policyPermissions, policyRoles, type PolicyInfo } from "./models";
import { PanelView, errorText, usePanel } from "../management/panel";

const t = managementText.staff.policies;
type Note = { tone: "success" | "error"; text: string };
type Scope = { permissionCode: string; roleCode: string };
type Confirmation = { question: string; done: string; run: () => Promise<void> };

const sameScope = (a: Scope, b: Scope) => a.permissionCode === b.permissionCode && a.roleCode === b.roleCode;

export function PoliciesPanel({ api, revision, onChanged, setNotice }: {
  api: StaffClient; revision: number; onChanged: () => void; setNotice: (note: Note | undefined) => void;
}) {
  const [data, reload] = usePanel<readonly PolicyInfo[]>(useCallback(() => api.listPolicies(), [api]), t.loadFailed, revision);
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState<Scope>();
  const [fields, setFields] = useState({ limit: "", count: "", hours: "" });
  const [confirmation, setConfirmation] = useState<Confirmation>();

  async function act(work: () => Promise<void>, success: string) {
    setBusy(true);
    try {
      await work();
      setNotice({ tone: "success", text: success });
      setConfirmation(undefined);
      setEditing(undefined);
      onChanged();
      reload();
    } catch (reason) {
      setNotice({ tone: "error", text: errorText(reason, t.actionFailed) });
    } finally {
      setBusy(false);
    }
  }

  function edit(scope: Scope, current?: PolicyInfo) {
    setNotice(undefined);
    setConfirmation(undefined);
    setEditing(scope);
    setFields(current
      ? { limit: String(current.limitAmount), count: String(current.maxCount), hours: String(current.windowSeconds / 3600) }
      : { limit: "", count: "", hours: "" });
  }

  function save(event: FormEvent, scope: Scope, current?: PolicyInfo) {
    event.preventDefault();
    const limitAmount = Number(fields.limit.replace(",", "."));
    const maxCount = Number(fields.count);
    const hours = Number(fields.hours);
    if (!(limitAmount > 0 && limitAmount <= 1000) || !Number.isInteger(maxCount) || maxCount < 1 || maxCount > 100
      || !Number.isInteger(hours) || hours < 1 || hours > 24) {
      return setNotice({ tone: "error", text: t.invalid });
    }
    setNotice(undefined);
    setConfirmation({
      question: t.saveQuestion(limitAmount, maxCount, hours),
      done: t.saved,
      run: () => api.setPolicy({ ...scope, limitAmount, maxCount, windowSeconds: hours * 3600 }, current?.rowVersion ?? null),
    });
  }

  return <PanelView title={t.heading} loading={t.loading} failed={t.loadFailed} panel={data} onRetry={reload}>
    {(policies) => <div>
      <p className="msr__hint">{t.hint}</p>
      <table>
        <thead><tr><th>{t.role}</th><th>{t.action}</th><th>{t.status}</th><th>{t.actions}</th></tr></thead>
        <tbody>{policyRoles.flatMap((role) => policyPermissions.map((permission) => {
          const scope = { permissionCode: permission.code, roleCode: role.code };
          const current = policies.find((policy) => sameScope(policy, scope));
          return <tr key={`${role.code}:${permission.code}`}>
            <td>{role.label}</td><td>{permission.label}</td>
            <td>{current ? t.auto(current.limitAmount, current.maxCount, current.windowSeconds / 3600) : t.manager}</td>
            <td><Button data-telemetry-id="b.management-staff-roles.policiespanel.1" variant="quiet" onClick={() => edit(scope, current)}>{t.edit}</Button></td>
          </tr>;
        }))}</tbody>
      </table>

      {editing && (() => {
        const current = policies.find((policy) => sameScope(policy, editing));
        return <form className="msr__form" aria-label={t.editHeading} onSubmit={(event) => save(event, editing, current)}>
          <h4>{t.editHeading}</h4>
          <TextField id="p-limit" label={t.limit} type="number" min={1} max={1000} step="0.01" value={fields.limit} onChange={(event) => setFields({ ...fields, limit: event.target.value })} />
          <TextField id="p-count" label={t.count} type="number" min={1} max={100} value={fields.count} onChange={(event) => setFields({ ...fields, count: event.target.value })} />
          <TextField id="p-hours" label={t.hours} type="number" min={1} max={24} value={fields.hours} onChange={(event) => setFields({ ...fields, hours: event.target.value })} />
          <div className="msr__confirm">
            <Button data-telemetry-id="b.management-staff-roles.policiespanel.2" type="submit" disabled={busy}>{t.save}</Button>
            {current && <Button data-telemetry-id="b.management-staff-roles.policiespanel.3" variant="secondary" disabled={busy} onClick={() => setConfirmation({
              question: t.removeQuestion, done: t.removed, run: () => api.removePolicy(editing.permissionCode, editing.roleCode),
            })}>{t.remove}</Button>}
            <Button data-telemetry-id="b.management-staff-roles.policiespanel.4" variant="secondary" onClick={() => { setEditing(undefined); setConfirmation(undefined); }}>{t.cancel}</Button>
          </div>
          {confirmation && <div className="msr__confirm" role="group" aria-label={confirmation.question}>
            <span>{confirmation.question}</span>
            <Button data-telemetry-id="b.management-staff-roles.policiespanel.5" disabled={busy} onClick={() => void act(confirmation.run, confirmation.done)}>{t.confirmYes}</Button>
            <Button data-telemetry-id="b.management-staff-roles.policiespanel.6" variant="secondary" onClick={() => setConfirmation(undefined)}>{t.confirmNo}</Button>
          </div>}
        </form>;
      })()}
    </div>}
  </PanelView>;
}
