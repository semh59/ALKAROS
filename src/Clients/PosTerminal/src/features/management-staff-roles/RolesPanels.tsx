import { useCallback, useState, type FormEvent } from "react";
import { Button, SelectField, TextField } from "../../design-system";
import { managementText } from "../../strings";
import type { StaffClient } from "./api";
import { permissionLabel, roleLabel, type PermissionInfo, type RoleInfo, type UserInfo } from "./models";
import { PanelView, errorText, usePanel } from "./panel";

const t = managementText.staff.roles;
type Note = { tone: "success" | "error"; text: string };
type PanelProps = { api: StaffClient; revision: number; onChanged: () => void; setNotice: (note: Note | undefined) => void };
type Confirmation = { question: string; run: () => Promise<void>; done: string };

function useActions(reload: () => void, setNotice: (note: Note | undefined) => void) {
  const [busy, setBusy] = useState(false);
  const [confirmation, setConfirmation] = useState<Confirmation>();
  async function act(work: () => Promise<void>, success: string) {
    setBusy(true);
    try {
      await work();
      setNotice({ tone: "success", text: success });
      setConfirmation(undefined);
      reload();
    } catch (reason) {
      setNotice({ tone: "error", text: errorText(reason, t.actionFailed) });
    } finally {
      setBusy(false);
    }
  }
  return { busy, confirmation, setConfirmation, act };
}

function ConfirmBar({ confirmation, busy, onYes, onNo }: { confirmation: Confirmation; busy: boolean; onYes: () => void; onNo: () => void }) {
  return <div className="msr__confirm" role="group" aria-label={confirmation.question}>
    <span>{confirmation.question}</span>
    <Button disabled={busy} onClick={onYes}>{t.confirmYes}</Button>
    <Button variant="secondary" onClick={onNo}>{t.confirmNo}</Button>
  </div>;
}

export function RolesPanel({ api, revision, onChanged, setNotice }: PanelProps) {
  const [data, reload] = usePanel<{ roles: readonly RoleInfo[]; permissions: readonly PermissionInfo[] }>(useCallback(
    () => Promise.all([api.listRoles(), api.listPermissions()]).then(([roles, permissions]) => ({ roles, permissions })), [api]), t.loadFailed, revision);
  const { busy, confirmation, setConfirmation, act } = useActions(onChanged, setNotice);
  const [editingId, setEditingId] = useState<string>();
  const [fields, setFields] = useState({ code: "", name: "" });

  function createRole(event: FormEvent) {
    event.preventDefault();
    const code = fields.code.trim();
    const name = fields.name.trim();
    if (!/^[a-z0-9-]+$/.test(code) || !name) return setNotice({ tone: "error", text: t.invalidRole });
    void act(async () => {
      await api.createRole(code, name);
      setFields({ code: "", name: "" });
    }, t.roleCreated);
  }

  return <PanelView title={t.heading} loading={t.loading} failed={t.loadFailed} panel={data} onRetry={reload}>
    {({ roles, permissions }) => {
      const editing = roles.find((role) => role.roleId === editingId);
      const sorted = [...permissions].sort((a, b) => permissionLabel(a.code).localeCompare(permissionLabel(b.code), "tr"));
      return <div>
        {roles.length === 0 ? <p className="msr__hint">{t.empty}</p> : <table>
          <thead><tr><th>{t.role}</th><th>{t.permissionCount}</th><th>{t.actions}</th></tr></thead>
          <tbody>{roles.map((role) => <tr key={role.roleId}>
            <td>{roleLabel(role)}</td><td>{role.permissionCodes.length}</td>
            <td><Button variant="quiet" onClick={() => { setNotice(undefined); setConfirmation(undefined); setEditingId(role.roleId); }}>{t.editPermissions}</Button></td></tr>)}</tbody>
        </table>}

        {editing && <fieldset className="msr__permissions">
          <legend>{roleLabel(editing)}: {t.permissions}</legend>
          {sorted.map((permission) => {
            const granted = editing.permissionCodes.includes(permission.code);
            return <label key={permission.code} className="msr__check">
              <input type="checkbox" checked={granted} disabled={busy} onChange={() => {
                setNotice(undefined);
                if (!granted) return void act(() => api.assignPermission(editing.roleId, permission.code), t.permissionGranted);
                setConfirmation({ question: `${permissionLabel(permission.code)}: ${t.revokeQuestion}`, done: t.permissionRevoked, run: () => api.revokePermission(editing.roleId, permission.code) });
              }} />
              {permissionLabel(permission.code)}
            </label>;
          })}
          {confirmation && <ConfirmBar confirmation={confirmation} busy={busy} onYes={() => void act(confirmation.run, confirmation.done)} onNo={() => setConfirmation(undefined)} />}
        </fieldset>}

        <form className="msr__form" aria-label={t.newRole} onSubmit={createRole}>
          <h4>{t.newRole}</h4>
          <TextField id="r-code" label={t.code} hint={t.codeHint} autoComplete="off" value={fields.code} onChange={(event) => setFields({ ...fields, code: event.target.value })} />
          <TextField id="r-name" label={t.name} autoComplete="off" value={fields.name} onChange={(event) => setFields({ ...fields, name: event.target.value })} />
          <Button type="submit" disabled={busy}>{t.createRole}</Button>
        </form>
      </div>;
    }}
  </PanelView>;
}

export function UserRolesPanel({ api, revision, onChanged, setNotice }: PanelProps) {
  const [data, reload] = usePanel<{ roles: readonly RoleInfo[]; users: readonly UserInfo[] }>(useCallback(
    () => Promise.all([api.listRoles(), api.listUsers()]).then(([roles, users]) => ({ roles, users })), [api]), t.loadFailed, revision);
  const { busy, confirmation, setConfirmation, act } = useActions(onChanged, setNotice);
  const [editingId, setEditingId] = useState<string>();
  const [pickedRole, setPickedRole] = useState("");

  return <PanelView title={t.usersHeading} loading={t.loading} failed={t.loadFailed} panel={data} onRetry={reload}>
    {({ roles, users }) => {
      const roleName = (id: string) => { const role = roles.find((candidate) => candidate.roleId === id); return role ? roleLabel(role) : t.unknownRole; };
      const editing = users.find((user) => user.userId === editingId);
      const free = editing ? roles.filter((role) => !editing.roleIds.includes(role.roleId)) : [];
      return <div>
        {users.length === 0 ? <p className="msr__hint">{t.noUsers}</p> : <table>
          <thead><tr><th>{t.person}</th><th>{t.username}</th><th>{t.status}</th><th>{t.userRoles}</th><th>{t.actions}</th></tr></thead>
          <tbody>{users.map((user) => <tr key={user.userId}>
            <td>{user.displayName}</td><td>{user.username}</td><td>{user.active ? t.active : t.inactive}</td>
            <td>{user.roleIds.length === 0 ? "—" : user.roleIds.map(roleName).join(", ")}</td>
            <td><Button variant="quiet" onClick={() => { setNotice(undefined); setConfirmation(undefined); setPickedRole(""); setEditingId(user.userId); }}>{t.editRoles}</Button></td></tr>)}</tbody>
        </table>}

        {editing && <div className="msr__form" role="group" aria-label={editing.displayName}>
          <h4>{editing.displayName}</h4>
          {editing.roleIds.map((id) => <div key={id} className="msr__check">
            <span>{roleName(id)}</span>
            <Button variant="quiet" disabled={busy} onClick={() => setConfirmation({ question: `${roleName(id)}: ${t.revokeRoleQuestion}`, done: t.roleRevoked, run: () => api.revokeUser(id, editing.userId) })}>{t.revokeRole}</Button>
          </div>)}
          {confirmation && <ConfirmBar confirmation={confirmation} busy={busy} onYes={() => void act(confirmation.run, confirmation.done)} onNo={() => setConfirmation(undefined)} />}
          <SelectField id="u-role" label={t.addRole} value={pickedRole} onChange={(event) => setPickedRole(event.target.value)}>
            <option value="">{t.pick}</option>
            {free.map((role) => <option key={role.roleId} value={role.roleId}>{roleLabel(role)}</option>)}
          </SelectField>
          <Button disabled={busy || !pickedRole} onClick={() => void act(async () => { await api.assignUser(pickedRole, editing.userId); setPickedRole(""); }, t.roleAssigned)}>{t.assign}</Button>
        </div>}
      </div>;
    }}
  </PanelView>;
}
