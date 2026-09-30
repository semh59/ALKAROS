import { useMemo, useState, type FormEvent } from "react";
import { Button, TextField } from "../../design-system";
import { managementText } from "../../strings";
import { createSecurityClient, SecurityApiError, type SecurityClient } from "./api";
import { fill, type UserLookup } from "./models";
import { Notice } from "./panel";
import "./management-security-system.css";

const t = managementText.security.recovery;

export function RecoveryPanel({ client }: { client?: SecurityClient }) {
  const api = useMemo(() => client ?? createSecurityClient(), [client]);
  const [username, setUsername] = useState("");
  const [busy, setBusy] = useState(false);
  const [found, setFound] = useState<UserLookup>();
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();

  async function run(work: () => Promise<void>) {
    setBusy(true);
    setNotice(undefined);
    try {
      await work();
    } catch (reason) {
      setNotice({ tone: "error", text: reason instanceof SecurityApiError ? reason.message : t.failed });
    } finally {
      setBusy(false);
    }
  }

  function search(event: FormEvent) {
    event.preventDefault();
    const name = username.trim();
    if (!name) return;
    setFound(undefined);
    void run(async () => {
      const user = await api.lookupUser(name);
      if (user) setFound(user);
      else setNotice({ tone: "error", text: t.notFound });
    });
  }

  return <section className="msys__panel" aria-label={t.heading} aria-busy={busy}>
    <h3>{t.heading}</h3>
    <p className="msys__hint">{t.hint}</p>
    <form className="msys__toolbar" onSubmit={search}>
      <TextField id="rec-username" label={t.username} autoComplete="off" value={username} onChange={(event) => setUsername(event.target.value)} />
      <Button type="submit" disabled={busy || !username.trim()}>{busy ? t.searching : t.search}</Button>
    </form>
    <Notice notice={notice} />
    {found && <div>
      <h4>{found.displayName}</h4>
      <p>{found.active ? t.active : t.inactive}{found.isLocked ? ` — ${t.locked}` : ""}</p>
      <div className="msys__actions">
        <Button disabled={busy} onClick={() => void run(async () => {
          const revoked = await api.revokeSessions(found.userId);
          setNotice({ tone: "success", text: fill(t.revoked, revoked) });
        })}>{t.revoke}</Button>
        <Button variant="secondary" disabled={busy || !found.isLocked} onClick={() => void run(async () => {
          await api.forceUnlock(found.userId);
          setFound({ ...found, isLocked: false });
          setNotice({ tone: "success", text: t.unlocked });
        })}>{t.unlock}</Button>
      </div>
    </div>}
  </section>;
}
