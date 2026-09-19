import { useCallback, useEffect, useRef, useState, type ChangeEvent, type FormEvent } from "react";
import { ApiError, api } from "../api";
import type { AccentPaletteEntry } from "../contracts";
import { savedId } from "../storage";
import "./business-identity-settings.css";

type StationSession = "checking" | "anonymous" | "forbidden" | "ready";

const SettingsManage = "settings.manage";
const BusinessNameKey = "business.name";
const BusinessAccentThemeKey = "business.accent_theme";
const AllowedLogoTypes = new Set(["image/png", "image/jpeg", "image/webp"]);
const MaxLogoBytes = 5 * 1024 * 1024;
const SaveReason = "PosTerminal ayarlar ekranından güncellendi";

/**
 * V1-CUI-012: Semih's own request that the business's own name and logo be
 * visible on its customer-facing pages (V1-SET-007/008/009).
 * `/settings/business-identity`, the same standalone-URL + own staff login
 * pattern as `/settings/relay` and `/settings/screensaver`.
 *
 * Unlike CustomerDisplayScreensaverSettings.tsx's own screen (whose read
 * endpoint is display-principal-only, so it can only ever show what THIS
 * session just uploaded), `GET /api/v1/qr/branding` and `GET /api/v1/qr/logo`
 * (V1-SET-007/008) are deliberately public — this screen fetches the real
 * server state on every load and prefills the form with it, not just this
 * session's own last edit.
 *
 * Reading /branding also self-registers business.name/business.accent_theme
 * (BusinessNameSetting/BusinessAccentThemeSetting's own "register on first
 * read" pattern) if this is a brand-new install where neither has ever been
 * read before — this always runs before the management GETs below, which
 * would otherwise 404 on a setting that has never been registered.
 */
export function BusinessIdentitySettings() {
  const [terminalId] = useState(() => savedId("alkaros.terminal-id"));
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [session, setSession] = useState<StationSession>("checking");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const [loaded, setLoaded] = useState(false);
  const [nameInput, setNameInput] = useState("");
  const [nameSaveMessage, setNameSaveMessage] = useState("");
  const [nameBusy, setNameBusy] = useState(false);

  const [paletteEntries, setPaletteEntries] = useState<AccentPaletteEntry[]>([]);
  const [selectedAccentKey, setSelectedAccentKey] = useState("");
  const [accentSaveMessage, setAccentSaveMessage] = useState("");
  const [accentBusy, setAccentBusy] = useState(false);

  const [currentLogoUrl, setCurrentLogoUrl] = useState<string | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [selectedPreviewUrl, setSelectedPreviewUrl] = useState<string | null>(null);
  const [fileError, setFileError] = useState("");
  const [logoMessage, setLogoMessage] = useState("");
  const [logoBusy, setLogoBusy] = useState(false);

  const currentLogoUrlRef = useRef<string | null>(null);
  const selectedPreviewUrlRef = useRef<string | null>(null);
  useEffect(() => { currentLogoUrlRef.current = currentLogoUrl; }, [currentLogoUrl]);
  useEffect(() => { selectedPreviewUrlRef.current = selectedPreviewUrl; }, [selectedPreviewUrl]);
  useEffect(() => () => {
    if (currentLogoUrlRef.current) URL.revokeObjectURL(currentLogoUrlRef.current);
    if (selectedPreviewUrlRef.current) URL.revokeObjectURL(selectedPreviewUrlRef.current);
  }, []);

  const loadCurrentState = useCallback(async () => {
    setError("");
    try {
      const [branding, palette, logo] = await Promise.all([
        api.qrBranding(),
        api.accentPalette(),
        api.fetchBusinessLogo(),
      ]);
      setNameInput(branding.businessName);
      setPaletteEntries(palette.entries);
      const matched = palette.entries.find((entry) => entry.hex.toLowerCase() === branding.accentColor.toLowerCase());
      setSelectedAccentKey(matched?.key ?? palette.defaultKey);
      if (currentLogoUrlRef.current) URL.revokeObjectURL(currentLogoUrlRef.current);
      setCurrentLogoUrl(logo?.url ?? null);
      setLoaded(true);
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : "Mevcut ayarlar okunamadı.");
    }
  }, []);

  const restoreSession = useCallback(async () => {
    try {
      const sessionInfo = await api.session(terminalId);
      if (!sessionInfo.capabilities?.includes(SettingsManage)) {
        setSession("forbidden");
        return;
      }
      setSession("ready");
      await loadCurrentState();
    } catch {
      setSession("anonymous");
    }
  }, [terminalId, loadCurrentState]);

  useEffect(() => { void restoreSession(); }, [restoreSession]);

  const login = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const result = await api.login(username, password, terminalId);
      setPassword("");
      if (!result.capabilities?.includes(SettingsManage)) {
        setSession("forbidden");
        return;
      }
      setSession("ready");
      await loadCurrentState();
    } catch (reason) {
      setSession("anonymous");
      setError(reason instanceof ApiError ? reason.message : "Giriş yapılamadı.");
    } finally {
      setBusy(false);
    }
  };

  const saveName = async (event: FormEvent) => {
    event.preventDefault();
    setNameBusy(true);
    setError("");
    setNameSaveMessage("");
    try {
      const record = await api.getSetting(BusinessNameKey);
      await api.updateSetting(BusinessNameKey, nameInput.trim(), record.rowVersion, SaveReason);
      setNameSaveMessage("İşletme adı kaydedildi.");
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : "Kaydedilemedi.");
    } finally {
      setNameBusy(false);
    }
  };

  const saveAccent = async (event: FormEvent) => {
    event.preventDefault();
    if (!selectedAccentKey) return;
    setAccentBusy(true);
    setError("");
    setAccentSaveMessage("");
    try {
      const record = await api.getSetting(BusinessAccentThemeKey);
      await api.updateSetting(BusinessAccentThemeKey, selectedAccentKey, record.rowVersion, SaveReason);
      setAccentSaveMessage("Renk kaydedildi.");
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : "Kaydedilemedi.");
    } finally {
      setAccentBusy(false);
    }
  };

  const handleFileChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null;
    setFileError("");
    setLogoMessage("");
    if (selectedPreviewUrl) URL.revokeObjectURL(selectedPreviewUrl);
    setSelectedFile(null);
    setSelectedPreviewUrl(null);
    if (!file) return;
    if (!AllowedLogoTypes.has(file.type)) {
      setFileError("Yalnız PNG, JPEG veya WEBP görseli yüklenebilir.");
      event.target.value = "";
      return;
    }
    if (file.size > MaxLogoBytes) {
      setFileError("Logo 5 MB'ı aşamaz.");
      event.target.value = "";
      return;
    }
    setSelectedFile(file);
    setSelectedPreviewUrl(URL.createObjectURL(file));
  };

  const uploadLogo = async (event: FormEvent) => {
    event.preventDefault();
    if (!selectedFile) return;
    setLogoBusy(true);
    setError("");
    setLogoMessage("");
    try {
      await api.uploadBusinessLogo(selectedFile);
      if (currentLogoUrl) URL.revokeObjectURL(currentLogoUrl);
      setCurrentLogoUrl(selectedPreviewUrl);
      setSelectedFile(null);
      setSelectedPreviewUrl(null);
      setLogoMessage("Logo yüklendi. QR sayfaları bir sonraki ziyarette bunu gösterecek.");
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : "Yüklenemedi.");
    } finally {
      setLogoBusy(false);
    }
  };

  const removeLogo = async () => {
    setLogoBusy(true);
    setError("");
    setLogoMessage("");
    try {
      await api.removeBusinessLogo();
      if (currentLogoUrl) URL.revokeObjectURL(currentLogoUrl);
      setCurrentLogoUrl(null);
      setLogoMessage("Logo kaldırıldı.");
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : "Kaldırılamadı.");
    } finally {
      setLogoBusy(false);
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
            <span>İşletme kimliği</span>
            <h2>Bu ekrana erişim yetkiniz yok</h2>
            <p>İşletme adını, rengini ve logosunu yalnızca yöneticiler değiştirebilir.</p>
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
            <span>İşletme kimliği</span>
            <h2>Yönetici girişi</h2>
            <p>İşletme adını, rengini ve logosunu değiştirmek için yönetici hesabınızla devam edin.</p>
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
      <section className="login-card" aria-busy={!loaded}>
        <div className="login-card-heading">
          <span>İşletme kimliği</span>
          <h2>İşletme Adı</h2>
          <p>QR menü sayfalarında müşteriye gösterilecek işletme adı.</p>
        </div>
        <form onSubmit={saveName}>
          <label>
            İşletme adı
            <input
              value={nameInput}
              onChange={(event) => setNameInput(event.target.value)}
              placeholder="Örn. Sahil Cafe"
              disabled={!loaded}
              autoComplete="off"
            />
          </label>
          {nameSaveMessage && <div className="alert information" role="status">{nameSaveMessage}</div>}
          <button className="primary login-submit" disabled={!loaded || nameBusy}>
            {nameBusy ? "Kaydediliyor…" : "Adı Kaydet"}
          </button>
        </form>
      </section>

      <section className="login-card" aria-busy={!loaded}>
        <div className="login-card-heading">
          <span>İşletme kimliği</span>
          <h2>Aksan Rengi</h2>
          <p>QR menü sayfalarında kullanılacak renk — hepsi kontrast testinden geçmiş, güvenli bir palet.</p>
        </div>
        <form onSubmit={saveAccent}>
          <div className="accent-swatch-grid" role="radiogroup" aria-label="Aksan rengi">
            {paletteEntries.map((entry) => (
              <label key={entry.key} className="accent-swatch">
                <input
                  type="radio"
                  name="accentColor"
                  value={entry.key}
                  checked={selectedAccentKey === entry.key}
                  onChange={() => setSelectedAccentKey(entry.key)}
                  disabled={!loaded}
                />
                <span className="accent-swatch-color" style={{ background: entry.hex }} />
                <span className="accent-swatch-label">{entry.label}</span>
              </label>
            ))}
          </div>
          {accentSaveMessage && <div className="alert information" role="status">{accentSaveMessage}</div>}
          <button className="primary login-submit" disabled={!loaded || accentBusy || !selectedAccentKey}>
            {accentBusy ? "Kaydediliyor…" : "Rengi Kaydet"}
          </button>
        </form>
      </section>

      <section className="login-card business-logo-card" aria-busy={!loaded}>
        <div className="login-card-heading">
          <span>İşletme kimliği</span>
          <h2>Logo</h2>
          <p>QR menü sayfalarının başlığında gösterilecek logo.</p>
        </div>
        <form onSubmit={uploadLogo}>
          <label>
            Logo (PNG, JPEG, WEBP — en fazla 5 MB)
            <input type="file" accept="image/png,image/jpeg,image/webp" onChange={handleFileChange} disabled={!loaded} />
          </label>
          {fileError && <div className="alert error" role="alert">{fileError}</div>}
          {selectedPreviewUrl && (
            <div className="business-logo-preview">
              <span>Seçilen logo önizlemesi:</span>
              <img src={selectedPreviewUrl} alt="Seçilen logo önizlemesi" />
            </div>
          )}
          {logoMessage && <div className="alert information" role="status">{logoMessage}</div>}
          <button className="primary login-submit" disabled={!loaded || logoBusy || !selectedFile}>
            {logoBusy ? "Yükleniyor…" : "Logoyu Yükle"}
          </button>
        </form>
        {currentLogoUrl && (
          <div className="business-logo-preview business-logo-preview-current">
            <span>Şu anda ayarlı logo:</span>
            <img src={currentLogoUrl} alt="QR menü sayfalarında gösterilen mevcut logo" />
            <button type="button" className="danger-text" disabled={logoBusy} onClick={() => void removeLogo()}>
              {logoBusy ? "Kaldırılıyor…" : "Kaldır"}
            </button>
          </div>
        )}
      </section>

      {error && <div className="alert error" role="alert">{error}</div>}
    </main>
  );
}
