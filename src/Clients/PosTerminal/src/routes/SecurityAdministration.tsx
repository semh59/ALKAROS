import { useCallback, useEffect, useState, type FormEvent } from "react";
import { ApiError, api } from "../api";
import { RecoveryPanel } from "../features/management-security-system";
import { savedId } from "../storage";

type StationSession = "checking" | "anonymous" | "forbidden" | "ready";

const SecurityManage = "security.manage";

/**
 * V1-RMD-331 (independent 2026-09-26 audit, finding K10 — the audit's own
 * "most critical" example): AccountRecoveryService (V15-SEC-002) and the
 * /api/v1/management/security/users/{id}/revoke-sessions + force-unlock
 * routes (V1-RMD-266) were real, tested and manager-gated on the server for
 * a while, but no client ever called them — a manager had no way to sign a
 * compromised account out everywhere or clear a lockout early except direct
 * SQL. `/settings/security`, same one-off-URL pattern as `/settings/relay`
 * and `/settings/token-terminal`: a manager who is already signed in on this
 * terminal already carries the alkaros.manager cookie these actions need
 * (minted at login for anyone holding catalog.manage — see
 * DualScreenApplication.Endpoints.cs), so this screen only has to call the
 * existing endpoints, never build a second login.
 */
export function SecurityAdministration() {
  const [terminalId] = useState(() => savedId("alkaros.terminal-id"));
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [session, setSession] = useState<StationSession>("checking");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const restoreSession = useCallback(async () => {
    try {
      const sessionInfo = await api.session(terminalId);
      if (!sessionInfo.capabilities?.includes(SecurityManage)) {
        setSession("forbidden");
        return;
      }
      setSession("ready");
    } catch (reason) {
      setSession(reason instanceof ApiError && reason.status === 401 ? "anonymous" : "anonymous");
    }
  }, [terminalId]);

  useEffect(() => {
    void restoreSession();
  }, [restoreSession]);

  const login = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const result = await api.login(username, password, terminalId);
      setPassword("");
      if (!result.capabilities?.includes(SecurityManage)) {
        setSession("forbidden");
        return;
      }
      setSession("ready");
    } catch (reason) {
      setSession("anonymous");
      setError(reason instanceof ApiError ? reason.message : "Giriş yapılamadı.");
    } finally {
      setBusy(false);
    }
  };

  if (session === "checking") {
    return <main className="login-shell"><p>Oturum kontrol ediliyor…</p></main>;
  }

  if (session === "forbidden") {
    return (
      <main className="login-shell">
        <section className="login-card">
          <div className="login-card-heading">
            <span>Güvenlik yönetimi</span>
            <h2>Bu ekrana erişim yetkiniz yok</h2>
            <p>Hesap kurtarma işlemlerini yalnızca yöneticiler yapabilir.</p>
          </div>
        </section>
      </main>
    );
  }

  if (session === "anonymous") {
    return (
      <main className="login-shell">
        <form className="login-card" onSubmit={login} aria-busy={busy}>
          <div className="login-card-heading">
            <span>Güvenlik yönetimi</span>
            <h2>Yönetici girişi</h2>
            <p>Hesap kurtarma işlemleri için yönetici hesabınızla devam edin.</p>
          </div>
          <label>
            Kullanıcı adı
            <input value={username} onChange={(event) => setUsername(event.target.value)} autoComplete="username" autoFocus />
          </label>
          <label>
            Parola
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
            />
          </label>
          {error && <div className="alert error" role="alert">{error}</div>}
          <button className="primary login-submit" disabled={busy}>{busy ? "Giriş yapılıyor…" : "Giriş yap"}</button>
        </form>
      </main>
    );
  }

  return (
    <main className="login-shell">
      <RecoveryPanel />
    </main>
  );
}
