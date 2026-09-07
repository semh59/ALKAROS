import { useCallback, useEffect, useState, type FormEvent } from "react";
import { ApiError, api } from "../api";
import { savedId } from "../storage";

type StationSession = "checking" | "anonymous" | "forbidden" | "ready";

const IntegrationsManage = "integrations.manage";

/**
 * V14-QRT-003. `/settings/relay` — its own URL for now, the same pattern as
 * `/reservations` and `/display`; it moves into the real back-office
 * navigation once that module is built. Lets a manager configure the relay
 * provider's API token entirely from the interface — no domain, no
 * Cloudflare account, no codebase — matching the "kolay B" model
 * (V14-QRT-002) and Semih's explicit request that the credential itself be
 * settable from here rather than baked into an installer.
 */
export function RelaySettings() {
  const [terminalId] = useState(() => savedId("alkaros.terminal-id"));
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [session, setSession] = useState<StationSession>("checking");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [status, setStatus] = useState<{ configured: boolean; updatedAt: string | null } | null>(null);
  const [tokenInput, setTokenInput] = useState("");
  const [saveMessage, setSaveMessage] = useState("");

  const loadStatus = useCallback(async () => {
    try {
      setStatus(await api.relayCredentialStatus(terminalId));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Durum alınamadı.");
    }
  }, [terminalId]);

  const restoreSession = useCallback(async () => {
    try {
      const sessionInfo = await api.session(terminalId);
      if (!sessionInfo.capabilities?.includes(IntegrationsManage)) {
        setSession("forbidden");
        return;
      }
      setSession("ready");
      await loadStatus();
    } catch (reason) {
      setSession(reason instanceof ApiError && reason.status === 401 ? "anonymous" : "anonymous");
    }
  }, [terminalId, loadStatus]);

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
      if (!result.capabilities?.includes(IntegrationsManage)) {
        setSession("forbidden");
        return;
      }
      setSession("ready");
      await loadStatus();
    } catch (reason) {
      setSession("anonymous");
      setError(reason instanceof Error ? reason.message : "Giriş yapılamadı.");
    } finally {
      setBusy(false);
    }
  };

  const save = async (event: FormEvent) => {
    event.preventDefault();
    if (!tokenInput.trim()) return;
    setBusy(true);
    setError("");
    setSaveMessage("");
    try {
      await api.saveRelayCredential(terminalId, tokenInput.trim());
      setTokenInput("");
      setSaveMessage("Bağlantı bilgisi kaydedildi.");
      await loadStatus();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Kaydedilemedi.");
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
            <span>Bağlantı ayarları</span>
            <h2>Bu ekrana erişim yetkiniz yok</h2>
            <p>Uzaktan sipariş bağlantısını yalnızca yöneticiler yapılandırabilir.</p>
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
            <span>Bağlantı ayarları</span>
            <h2>Yönetici girişi</h2>
            <p>Uzaktan sipariş bağlantısını yapılandırmak için yönetici hesabınızla devam edin.</p>
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
      <section className="login-card" aria-busy={busy}>
        <div className="login-card-heading">
          <span>Bağlantı ayarları</span>
          <h2>Uzaktan Sipariş Bağlantısı</h2>
          <p>
            {status?.configured
              ? "● Yapılandırıldı" + (status.updatedAt ? ` — son güncelleme: ${new Date(status.updatedAt).toLocaleString("tr-TR")}` : "")
              : "● Henüz yapılandırılmadı"}
          </p>
        </div>
        <form onSubmit={save}>
          <label>
            Bağlantı anahtarı
            <input
              type="password"
              value={tokenInput}
              onChange={(event) => setTokenInput(event.target.value)}
              autoComplete="off"
              placeholder={status?.configured ? "Değiştirmek için yeni anahtarı girin" : "Anahtarı buraya yapıştırın"}
            />
          </label>
          {error && <div className="alert error" role="alert">{error}</div>}
          {saveMessage && <div className="alert information" role="status">{saveMessage}</div>}
          <button className="primary login-submit" disabled={busy || !tokenInput.trim()}>
            {busy ? "Kaydediliyor…" : "Kaydet"}
          </button>
        </form>
      </section>
    </main>
  );
}
