import { useCallback, useMemo, useState, type FormEvent } from "react";
import { Button, TextField } from "../../design-system";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { createStaffClient, type StaffClient, type UserLookup } from "./api";
import { PoliciesPanel } from "./PoliciesPanel";
import { RolesPanel, UserRolesPanel } from "./RolesPanels";
import "./management-staff-roles.css";
import { ManagementApiError } from "../management/http";

const t = managementText.staff;
const minimumPasswordLength = 8;

export function StaffSection({ capabilities, client }: ManagementSectionProps & { client?: StaffClient }) {
  const api = useMemo(() => client ?? createStaffClient(), [client]);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();
  const [busy, setBusy] = useState(false);
  const [fields, setFields] = useState<Record<string, string>>({});
  const [found, setFound] = useState<UserLookup | null>();
  const [lookedUp, setLookedUp] = useState("");
  const [confirmingDeactivate, setConfirmingDeactivate] = useState(false);
  const [revision, setRevision] = useState(0);
  const changed = useCallback(() => setRevision((current) => current + 1), []);
  const field = (key: string) => fields[key] ?? "";
  const set = (key: string) => (event: { target: { value: string } }) => setFields((current) => ({ ...current, [key]: event.target.value }));

  async function act(work: () => Promise<void>, success?: string) {
    setBusy(true);
    try {
      await work();
      if (success) setNotice({ tone: "success", text: success });
    } catch (reason) {
      setNotice({ tone: "error", text: reason instanceof ManagementApiError ? reason.message : t.actionFailed });
    } finally {
      setBusy(false);
    }
  }

  function submitCreate(event: FormEvent) {
    event.preventDefault();
    const username = field("username").trim();
    const displayName = field("displayName").trim();
    if (!username || !displayName || field("password").length < minimumPasswordLength) return setNotice({ tone: "error", text: t.invalidNewUser });
    void act(async () => {
      await api.createUser(username, field("password"), displayName);
      setFields({});
      changed();
    }, t.created);
  }

  function toggleActive(user: UserLookup) {
    setConfirmingDeactivate(false);
    return act(async () => {
      await api.setActive(user.userId, !user.active);
      setFound(await api.lookupUser(lookedUp));
      changed();
    }, user.active ? t.deactivated : t.reactivated);
  }

  function search(username = field("search").trim()) {
    if (!username) return;
    setNotice(undefined);
    setConfirmingDeactivate(false);
    setLookedUp(username);
    void act(async () => setFound(await api.lookupUser(username)));
  }

  return <div className="msr">
    {notice && <div role={notice.tone === "error" ? "alert" : "status"} className={`management__notice management__notice--${notice.tone}`}>{notice.text}</div>}

    <section className="management__panel" aria-label={t.createHeading}>
      <h3>{t.createHeading}</h3>
      <p className="msr__hint">{t.createHint}</p>
      <form className="msr__form" aria-label={t.createHeading} onSubmit={submitCreate}>
        <TextField id="u-username" label={t.username} autoComplete="off" value={field("username")} onChange={set("username")} />
        <TextField id="u-display" label={t.displayName} autoComplete="off" value={field("displayName")} onChange={set("displayName")} />
        <TextField id="u-password" label={t.password} type="password" autoComplete="new-password" hint={t.passwordHint} value={field("password")} onChange={set("password")} />
        <Button data-telemetry-id="b.management-staff-roles.staffsection.1" type="submit" disabled={busy}>{t.create}</Button>
      </form>
    </section>

    {capabilities.has("security.manage") && <section className="management__panel" aria-label={t.findHeading}>
      <h3>{t.findHeading}</h3>
      <form className="msr__toolbar" aria-label={t.findHeading} onSubmit={(event) => { event.preventDefault(); search(); }}>
        <TextField id="u-search" label={t.username} autoComplete="off" value={field("search")} onChange={set("search")} />
        <Button data-telemetry-id="b.management-staff-roles.staffsection.2" type="submit" variant="secondary" disabled={busy}>{t.find}</Button>
      </form>
      {found === null && <p className="msr__hint">{t.notFound}</p>}
      {found && <div>
        <p><strong>{found.displayName}</strong> · {found.active ? t.active : t.inactive}{found.isLocked ? ` · ${t.locked}` : ""}</p>
        {confirmingDeactivate
          ? <div className="msr__confirm" role="group" aria-label={t.deactivateQuestion}>
            <span>{t.deactivateQuestion}</span>
            <Button data-telemetry-id="b.management-staff-roles.staffsection.4" disabled={busy} onClick={() => void toggleActive(found)}>{t.deactivateYes}</Button>
            <Button data-telemetry-id="b.management-staff-roles.staffsection.5" variant="secondary" onClick={() => setConfirmingDeactivate(false)}>{t.roles.confirmNo}</Button>
          </div>
          : <Button data-telemetry-id="b.management-staff-roles.staffsection.3" variant="secondary" disabled={busy} onClick={() => found.active ? setConfirmingDeactivate(true) : void toggleActive(found)}>{found.active ? t.deactivate : t.reactivate}</Button>}
      </div>}
    </section>}

    {capabilities.has("identity.roles.manage") && <RolesPanel api={api} revision={revision} onChanged={changed} setNotice={setNotice} />}
    {capabilities.has("identity.roles.manage") && <UserRolesPanel api={api} revision={revision} onChanged={changed} setNotice={setNotice} />}
    {capabilities.has("identity.roles.manage") && <PoliciesPanel api={api} revision={revision} onChanged={changed} setNotice={setNotice} />}
  </div>;
}
