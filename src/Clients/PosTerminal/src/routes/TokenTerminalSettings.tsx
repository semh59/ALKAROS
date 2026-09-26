import { useCallback, useEffect, useState, type FormEvent } from "react";
import { ApiError, api } from "../api";
import type { TokenTerminalCredentialStatus } from "../contracts";
import { savedId } from "../storage";

type StationSession = "checking" | "anonymous" | "forbidden" | "ready";

const IntegrationsManage = "integrations.manage";

// V1-RMD-342 (independent 2026-09-26 audit, orta seviye bulgu): mirrors the
// server's own check (TokenTerminalSettingsEndpoints.IsValidTokenTerminalId)
// - matches this screen's own label/placeholder promise (an "AV"/"AT"
// prefix, e.g. "AV0000111044"). merchantId/branchId have no similarly
// confirmed format in this codebase's own research to validate against.
function isValidTokenTerminalId(value: string): boolean {
  if (value.length < 3) return false;
  const prefix = value.slice(0, 2).toUpperCase();
  return (prefix === "AV" || prefix === "AT") && /^[0-9]+$/.test(value.slice(2));
}

/**
 * V13-HUG-005. Mirrors `RelaySettings.tsx`'s exact screen shape (login,
 * status, form) for `V12-QRT-003`'s Cloudflare token. Lets a manager
 * register the Token/Beko terminal's `merchant-id`/`branch-id`/
 * `terminal-id` (printed on the physical fiscal device, prefixed `AV`/`AT`, or read
 * from the TokenX Connect app's QR code as `merchantId_branchId_terminalId`
 * — see `evidence/v0/integrations/V0-HUG-001/` for how this was found) and
 * `client-id`/`client-secret` (obtained once from Token as a software
 * vendor, shared across every deployment). Does NOT call Token's live
 * API — see `V13-HUG-005`'s own Goal.
 */
export function TokenTerminalSettings() {
  const [terminalId] = useState(() => savedId("alkaros.terminal-id"));
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [session, setSession] = useState<StationSession>("checking");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [status, setStatus] = useState<TokenTerminalCredentialStatus | null>(null);
  const [qrInput, setQrInput] = useState("");
  const [merchantIdInput, setMerchantIdInput] = useState("");
  const [branchIdInput, setBranchIdInput] = useState("");
  const [tokenTerminalIdInput, setTokenTerminalIdInput] = useState("");
  const [clientIdInput, setClientIdInput] = useState("");
  const [clientSecretInput, setClientSecretInput] = useState("");
  const [saveMessage, setSaveMessage] = useState("");

  const loadStatus = useCallback(async () => {
    try {
      const result = await api.tokenTerminalCredentialStatus(terminalId);
      setStatus(result);
      setMerchantIdInput(result.merchantId ?? "");
      setBranchIdInput(result.branchId ?? "");
      setTokenTerminalIdInput(result.terminalId ?? "");
      setClientIdInput(result.clientId ?? "");
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

  // Token's own QR code format, "merchantId_branchId_terminalId" (found in
  // developer.tokeninc.com's SSS — see evidence/v0/integrations/V0-HUG-001/),
  // so a manager can paste one scan result instead of typing three fields.
  const applyQrCode = (raw: string) => {
    setQrInput(raw);
    const parts = raw.trim().split("_");
    if (parts.length === 3 && parts.every((part) => part.length > 0)) {
      setMerchantIdInput(parts[0]);
      setBranchIdInput(parts[1]);
      setTokenTerminalIdInput(parts[2]);
    }
  };

  const trimmedTokenTerminalId = tokenTerminalIdInput.trim();
  const tokenTerminalIdInvalid = trimmedTokenTerminalId.length > 0 && !isValidTokenTerminalId(trimmedTokenTerminalId);
  const canSave = Boolean(
    merchantIdInput.trim() && branchIdInput.trim() && isValidTokenTerminalId(trimmedTokenTerminalId)
    && clientIdInput.trim() && clientSecretInput.trim());

  const save = async (event: FormEvent) => {
    event.preventDefault();
    if (!canSave) return;
    setBusy(true);
    setError("");
    setSaveMessage("");
    try {
      await api.saveTokenTerminalCredential(
        terminalId,
        merchantIdInput.trim(),
        branchIdInput.trim(),
        tokenTerminalIdInput.trim(),
        clientIdInput.trim(),
        clientSecretInput.trim(),
      );
      setClientSecretInput("");
      setQrInput("");
      setSaveMessage("Terminal bilgileri kaydedildi.");
      await loadStatus();
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : "Kaydedilemedi.");
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
            <span>Ödeme terminali ayarları</span>
            <h2>Bu ekrana erişim yetkiniz yok</h2>
            <p>Token/Beko terminal bağlantısını yalnızca yöneticiler yapılandırabilir.</p>
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
            <span>Ödeme terminali ayarları</span>
            <h2>Yönetici girişi</h2>
            <p>Token/Beko terminal bağlantısını yapılandırmak için yönetici hesabınızla devam edin.</p>
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
          <span>Ödeme terminali ayarları</span>
          <h2>Token/Beko Terminal Bağlantısı</h2>
          <p>
            {status?.configured
              ? "● Yapılandırıldı" + (status.updatedAt ? ` — son güncelleme: ${new Date(status.updatedAt).toLocaleString("tr-TR")}` : "")
              : "● Henüz yapılandırılmadı"}
          </p>
        </div>
        <form onSubmit={save}>
          <label>
            QR kod (opsiyonel — cihazın TokenX Connect uygulamasından okutulan kod)
            <input
              value={qrInput}
              onChange={(event) => applyQrCode(event.target.value)}
              autoComplete="off"
              placeholder="merchantId_branchId_terminalId"
            />
          </label>
          <label>
            İşletme kimliği (Merchant ID)
            <input value={merchantIdInput} onChange={(event) => setMerchantIdInput(event.target.value)} autoComplete="off" />
          </label>
          <label>
            Şube kimliği (Branch ID)
            <input value={branchIdInput} onChange={(event) => setBranchIdInput(event.target.value)} autoComplete="off" />
          </label>
          <label>
            Terminal kimliği (cihazın arkasında, AV/AT ile başlar)
            <input
              value={tokenTerminalIdInput}
              onChange={(event) => setTokenTerminalIdInput(event.target.value)}
              autoComplete="off"
              placeholder="AV0000111044"
              aria-invalid={tokenTerminalIdInvalid}
            />
          </label>
          {tokenTerminalIdInvalid && (
            <div className="alert error" role="alert">Terminal kimliği 'AV' veya 'AT' ile başlamalı ve ardından yalnızca rakam içermelidir.</div>
          )}
          <label>
            Müşteri kimliği (Client ID)
            <input value={clientIdInput} onChange={(event) => setClientIdInput(event.target.value)} autoComplete="off" />
          </label>
          <label>
            Gizli anahtar (Client Secret)
            <input
              type="password"
              value={clientSecretInput}
              onChange={(event) => setClientSecretInput(event.target.value)}
              autoComplete="off"
              placeholder={status?.configured ? "Değiştirmek için yeni anahtarı girin" : "Anahtarı buraya yapıştırın"}
            />
          </label>
          {error && <div className="alert error" role="alert">{error}</div>}
          {saveMessage && <div className="alert information" role="status">{saveMessage}</div>}
          <button className="primary login-submit" disabled={busy || !canSave}>
            {busy ? "Kaydediliyor…" : "Kaydet"}
          </button>
        </form>
      </section>
    </main>
  );
}
