import { useCallback, useEffect, useState, type FormEvent } from "react";
import { ApiError, api } from "../api";
import type { QnbConnectionTestResult, QnbCredentialStatus } from "../contracts";
import { savedId } from "../storage";

type StationSession = "checking" | "anonymous" | "forbidden" | "ready";

const IntegrationsManage = "integrations.manage";

// V1-RMD-342 (independent 2026-09-26 audit, orta seviye bulgu): mirrors the
// server's own check (QnbCredentialSettingsEndpoints.IsValidVergiTcKimlikNo)
// so an obviously-wrong value is caught here instead of round-tripping to
// the server first. The Turkish tax authority's own tax id convention (10
// digits for a legal entity, 11 for an individual taxpayer) - both
// all-digit, fixed length, no checksum.
function isValidVergiTcKimlikNo(value: string): boolean {
  return (value.length === 10 || value.length === 11) && /^[0-9]+$/.test(value);
}

/**
 * V14-QNB-006. Mirrors `TokenTerminalSettings.tsx`'s exact screen shape
 * (login, status, form). Lets a manager register QNB eSolutions e-Fatura
 * credentials: `userId`/`password` (QNB's own SOAP `wsLogin` session
 * auth) and `vergiTcKimlikNo` (VKN — required on every `belgeGonderExt`/
 * status call, see `evidence/v0/integrations/V0-QNB-001/**`). Does NOT
 * call QNB's live API — see `V14-QNB-006`'s own Goal.
 */
export function QnbCredentialSettings() {
  const [terminalId] = useState(() => savedId("alkaros.terminal-id"));
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [session, setSession] = useState<StationSession>("checking");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [status, setStatus] = useState<QnbCredentialStatus | null>(null);
  const [userIdInput, setUserIdInput] = useState("");
  const [passwordInput, setPasswordInput] = useState("");
  const [vergiTcKimlikNoInput, setVergiTcKimlikNoInput] = useState("");
  const [saveMessage, setSaveMessage] = useState("");
  const [testBusy, setTestBusy] = useState(false);
  const [testResult, setTestResult] = useState<QnbConnectionTestResult | null>(null);

  const loadStatus = useCallback(async () => {
    try {
      const result = await api.qnbCredentialStatus(terminalId);
      setStatus(result);
      setUserIdInput(result.userId ?? "");
      setVergiTcKimlikNoInput(result.vergiTcKimlikNo ?? "");
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : "Durum alınamadı.");
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
      setError(reason instanceof ApiError ? reason.message : "Giriş yapılamadı.");
    } finally {
      setBusy(false);
    }
  };

  const trimmedVergiTcKimlikNo = vergiTcKimlikNoInput.trim();
  const vergiTcKimlikNoInvalid = trimmedVergiTcKimlikNo.length > 0 && !isValidVergiTcKimlikNo(trimmedVergiTcKimlikNo);
  const canSave = Boolean(userIdInput.trim() && passwordInput.trim() && isValidVergiTcKimlikNo(trimmedVergiTcKimlikNo));

  const save = async (event: FormEvent) => {
    event.preventDefault();
    if (!canSave) return;
    setBusy(true);
    setError("");
    setSaveMessage("");
    try {
      await api.saveQnbCredential(terminalId, userIdInput.trim(), passwordInput.trim(), vergiTcKimlikNoInput.trim());
      setPasswordInput("");
      setSaveMessage("QNB e-Fatura bilgileri kaydedildi.");
      await loadStatus();
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : "Kaydedilemedi.");
    } finally {
      setBusy(false);
    }
  };

  const testConnection = async () => {
    setTestBusy(true);
    setTestResult(null);
    try {
      const result = await api.testQnbConnection(terminalId);
      setTestResult(result);
    } catch (reason) {
      setTestResult({ success: false, message: reason instanceof ApiError ? reason.message : "Bağlantı denenemedi." });
    } finally {
      setTestBusy(false);
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
            <span>e-Fatura ayarları</span>
            <h2>Bu ekrana erişim yetkiniz yok</h2>
            <p>QNB e-Fatura bağlantısını yalnızca yöneticiler yapılandırabilir.</p>
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
            <span>e-Fatura ayarları</span>
            <h2>Yönetici girişi</h2>
            <p>QNB e-Fatura bağlantısını yapılandırmak için yönetici hesabınızla devam edin.</p>
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
          <span>e-Fatura ayarları</span>
          <h2>QNB e-Fatura Bağlantısı</h2>
          <p>
            {status?.configured
              ? "● Yapılandırıldı" + (status.updatedAt ? ` — son güncelleme: ${new Date(status.updatedAt).toLocaleString("tr-TR")}` : "")
              : "● Henüz yapılandırılmadı"}
          </p>
        </div>
        <form onSubmit={save}>
          <label>
            QNB kullanıcı adı (User ID)
            <input value={userIdInput} onChange={(event) => setUserIdInput(event.target.value)} autoComplete="off" />
          </label>
          <label>
            QNB parolası (Password)
            <input
              type="password"
              value={passwordInput}
              onChange={(event) => setPasswordInput(event.target.value)}
              autoComplete="off"
              placeholder={status?.configured ? "Değiştirmek için yeni parolayı girin" : "Parolayı buraya yapıştırın"}
            />
          </label>
          <label>
            Vergi kimlik numarası (VKN)
            <input
              value={vergiTcKimlikNoInput}
              onChange={(event) => setVergiTcKimlikNoInput(event.target.value)}
              autoComplete="off"
              placeholder="3250566851"
              aria-invalid={vergiTcKimlikNoInvalid}
            />
          </label>
          {vergiTcKimlikNoInvalid && (
            <div className="alert error" role="alert">Vergi kimlik numarası 10 veya 11 haneli, yalnızca rakamlardan oluşmalıdır.</div>
          )}
          {error && <div className="alert error" role="alert">{error}</div>}
          {saveMessage && <div className="alert information" role="status">{saveMessage}</div>}
          <button className="primary login-submit" disabled={busy || !canSave}>
            {busy ? "Kaydediliyor…" : "Kaydet"}
          </button>
        </form>
        {status?.configured && (
          <>
            <button
              type="button"
              className="secondary login-submit"
              disabled={testBusy}
              onClick={() => void testConnection()}
            >
              {testBusy ? "Bağlantı deneniyor…" : "Bağlantıyı Test Et"}
            </button>
            {testResult && (
              <div className={`alert ${testResult.success ? "information" : "error"}`} role="status">
                {testResult.message}
              </div>
            )}
          </>
        )}
      </section>
    </main>
  );
}
