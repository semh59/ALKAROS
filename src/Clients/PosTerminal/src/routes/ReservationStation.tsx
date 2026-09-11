import { useCallback, useEffect, useState, type FormEvent } from "react";
import { ApiError, api } from "../api";
import { RouterProvider } from "../router";
import { savedId } from "../storage";
import { TableRoute } from "./workspace";

type StationSession = "checking" | "anonymous" | "disabled" | "ready";

/**
 * V1-CUI-006: the dedicated Reservation Station screen — a lean `/reservations`
 * view for a business with dedicated reservation staff, reached on its own
 * device (think of it like the customer display: its own URL, its own
 * chrome, no billing/sales/kitchen tabs). Only offered when the deployment
 * has turned the V1-SET-003 setting on; otherwise the same staff member
 * uses the "Rezervasyon al" action already built into the cashier's own
 * floor-plan screen — that action is unconditional and unaffected by this
 * screen existing or not.
 *
 * Unlike the customer display (unauthenticated, paired), this screen
 * performs privileged table actions, so it reuses the same staff login as
 * the main Cashier app. Whichever role a deployment assigns to a
 * reservation station operator, the "canManage" flag stays false here —
 * zone/table setup and starting orders remain the cashier's own screen's
 * job; this screen only ever shows the reservation actions the server's
 * `AllowedCommands` already scopes to whatever permission that operator's
 * role actually holds (no new permission code was needed for this).
 */
export function ReservationStation() {
  const [terminalId] = useState(() => savedId("alkaros.terminal-id"));
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [session, setSession] = useState<StationSession>("checking");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const restoreSession = useCallback(async () => {
    try {
      const [sessionInfo, config] = await Promise.all([api.session(terminalId), api.runtimeConfig(terminalId)]);
      setDisplayName(sessionInfo.displayName);
      setSession(config.reservationStationEnabled ? "ready" : "disabled");
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
      setDisplayName(result.displayName);
      setPassword("");
      await restoreSession();
    } catch (reason) {
      setSession("anonymous");
      setError(reason instanceof ApiError ? reason.message : "Giriş yapılamadı.");
    } finally {
      setBusy(false);
    }
  };

  const logout = async () => {
    setBusy(true);
    try {
      await api.logout(terminalId);
    } finally {
      setSession("anonymous");
      setDisplayName("");
      setBusy(false);
    }
  };

  if (session === "checking") {
    return <main className="login-shell"><p>Oturum kontrol ediliyor…</p></main>;
  }

  if (session === "disabled") {
    return (
      <main className="login-shell">
        <section className="login-card">
          <div className="login-card-heading">
            <span>Rezervasyon istasyonu</span>
            <h2>Bu ekran etkin değil</h2>
            <p>
              Bu işletmede ayrı bir rezervasyon istasyonu tanımlı değil. Rezervasyon almak için
              kasa ekranındaki kat planından "Rezervasyon al" işlemini kullanın.
            </p>
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
            <span>Rezervasyon istasyonu</span>
            <h2>Personel girişi</h2>
            <p>Rezervasyon yetkili hesabınızla devam edin.</p>
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
    <main className="reservation-station-shell">
      <header className="pos-header">
        <div className="pos-brand">
          <div className="brand-mark compact">A</div>
          <div><strong>ALKAROS</strong><span>Rezervasyon istasyonu</span></div>
        </div>
        <div className="pos-header-actions">
          <div className="operator-card"><span>{displayName || username}</span><small>Rezervasyon</small></div>
          <button className="icon-action" disabled={busy} onClick={() => void logout()} aria-label="Oturumu kapat">
            Çıkış
          </button>
        </div>
      </header>
      <RouterProvider>
        <TableRoute terminalId={terminalId} canManage={false} />
      </RouterProvider>
    </main>
  );
}
