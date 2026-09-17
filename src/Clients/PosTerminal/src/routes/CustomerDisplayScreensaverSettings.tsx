import { useCallback, useEffect, useRef, useState, type ChangeEvent, type FormEvent } from "react";
import { ApiError, api } from "../api";
import { savedId } from "../storage";
import "./customer-display-screensaver-settings.css";

type StationSession = "checking" | "anonymous" | "forbidden" | "ready";

const CatalogManage = "catalog.manage";
const AllowedImageTypes = new Set(["image/png", "image/jpeg", "image/webp"]);
const AllowedVideoTypes = new Set(["video/mp4"]);
const MaxImageBytes = 5 * 1024 * 1024;
// V1-CDP-004: a separate, larger cap for video — Semih's own call, not the
// image limit simply raised (a 6 MB image is still rejected).
const MaxVideoBytes = 20 * 1024 * 1024;

/**
 * V1-CDP-003: Semih's own request — the business should be able to set its
 * own idle-screen image. Lets it upload/replace/remove the customer
 * display's Idle-screen media (V1-CDP-001's storage, V1-CDP-002's render).
 * `/settings/screensaver`, the same standalone-URL + own-login pattern as
 * `/settings/relay` (RelaySettings.tsx) rather than a ProductionShell tab —
 * this is a rare, one-off configuration action, not a screen anyone works
 * from all shift. V1-CDP-004: also accepts a short looping video
 * (`video/mp4`, its own 20 MB cap — a still image stays capped at 5 MB).
 *
 * There is no manager-facing "what's currently set" endpoint (V1-CDP-001's
 * GET is display-principal-only, by design — a screensaver is per-business,
 * not something a manager session can already read back). So "the current
 * media" shown here only ever reflects what THIS session just uploaded or
 * removed, not a fresh fact fetched from the server on page load — the
 * copy below says so plainly rather than implying it is live.
 */
export function CustomerDisplayScreensaverSettings() {
  const [terminalId] = useState(() => savedId("alkaros.terminal-id"));
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [session, setSession] = useState<StationSession>("checking");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [selectedPreviewUrl, setSelectedPreviewUrl] = useState<string | null>(null);
  const [selectedIsVideo, setSelectedIsVideo] = useState(false);
  const [fileError, setFileError] = useState("");

  const [currentMediaUrl, setCurrentMediaUrl] = useState<string | null>(null);
  const [currentMediaIsVideo, setCurrentMediaIsVideo] = useState(false);
  const [statusMessage, setStatusMessage] = useState("");

  const selectedPreviewUrlRef = useRef<string | null>(null);
  const currentMediaUrlRef = useRef<string | null>(null);
  useEffect(() => { selectedPreviewUrlRef.current = selectedPreviewUrl; }, [selectedPreviewUrl]);
  useEffect(() => { currentMediaUrlRef.current = currentMediaUrl; }, [currentMediaUrl]);
  useEffect(() => () => {
    if (selectedPreviewUrlRef.current) URL.revokeObjectURL(selectedPreviewUrlRef.current);
    if (currentMediaUrlRef.current) URL.revokeObjectURL(currentMediaUrlRef.current);
  }, []);

  const restoreSession = useCallback(async () => {
    try {
      const sessionInfo = await api.session(terminalId);
      setSession(sessionInfo.capabilities?.includes(CatalogManage) ? "ready" : "forbidden");
    } catch {
      setSession("anonymous");
    }
  }, [terminalId]);

  useEffect(() => { void restoreSession(); }, [restoreSession]);

  const login = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const result = await api.login(username, password, terminalId);
      setPassword("");
      setSession(result.capabilities?.includes(CatalogManage) ? "ready" : "forbidden");
    } catch (reason) {
      setSession("anonymous");
      setError(reason instanceof ApiError ? reason.message : "Giriş yapılamadı.");
    } finally {
      setBusy(false);
    }
  };

  const handleFileChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null;
    setFileError("");
    setStatusMessage("");
    if (selectedPreviewUrl) URL.revokeObjectURL(selectedPreviewUrl);
    setSelectedFile(null);
    setSelectedPreviewUrl(null);
    setSelectedIsVideo(false);
    if (!file) return;
    const isImage = AllowedImageTypes.has(file.type);
    const isVideo = AllowedVideoTypes.has(file.type);
    if (!isImage && !isVideo) {
      setFileError("Yalnız PNG, JPEG, WEBP görseli veya MP4 videosu yüklenebilir.");
      event.target.value = "";
      return;
    }
    if (isImage && file.size > MaxImageBytes) {
      setFileError("Görsel 5 MB'ı aşamaz.");
      event.target.value = "";
      return;
    }
    if (isVideo && file.size > MaxVideoBytes) {
      setFileError("Video 20 MB'ı aşamaz.");
      event.target.value = "";
      return;
    }
    setSelectedFile(file);
    setSelectedPreviewUrl(URL.createObjectURL(file));
    setSelectedIsVideo(isVideo);
  };

  const upload = async (event: FormEvent) => {
    event.preventDefault();
    if (!selectedFile) return;
    setBusy(true);
    setError("");
    setStatusMessage("");
    try {
      await api.uploadScreensaver(selectedFile);
      if (currentMediaUrl) URL.revokeObjectURL(currentMediaUrl);
      setCurrentMediaUrl(selectedPreviewUrl);
      setCurrentMediaIsVideo(selectedIsVideo);
      setSelectedFile(null);
      setSelectedPreviewUrl(null);
      setSelectedIsVideo(false);
      setStatusMessage(
        selectedIsVideo
          ? "Ekran koruyucu videosu yüklendi. Müşteri ekranı bir sonraki boşta kalışında bunu gösterecek."
          : "Ekran koruyucu görseli yüklendi. Müşteri ekranı bir sonraki boşta kalışında bunu gösterecek.",
      );
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : "Yüklenemedi.");
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    setBusy(true);
    setError("");
    setStatusMessage("");
    try {
      await api.removeScreensaver();
      if (currentMediaUrl) URL.revokeObjectURL(currentMediaUrl);
      setCurrentMediaUrl(null);
      setCurrentMediaIsVideo(false);
      setStatusMessage("Ekran koruyucu kaldırıldı. Müşteri ekranı varsayılan markalı karta döndü.");
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : "Kaldırılamadı.");
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
            <span>Ekran koruyucu</span>
            <h2>Bu ekrana erişim yetkiniz yok</h2>
            <p>Müşteri ekranının ekran koruyucusunu yalnızca yöneticiler değiştirebilir.</p>
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
            <span>Ekran koruyucu</span>
            <h2>Yönetici girişi</h2>
            <p>Müşteri ekranının ekran koruyucusunu değiştirmek için yönetici hesabınızla devam edin.</p>
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
      <section className="login-card screensaver-settings-card" aria-busy={busy}>
        <div className="login-card-heading">
          <span>Ekran koruyucu</span>
          <h2>Müşteri Ekranı Ekran Koruyucusu</h2>
          <p>Müşteri ekranı boşta iken (sipariş beklerken) gösterilecek görsel veya videoyu buradan yükleyin ya da kaldırın.</p>
        </div>
        <form onSubmit={upload}>
          <label>
            Görsel (PNG, JPEG, WEBP — en fazla 5 MB) veya video (MP4 — en fazla 20 MB)
            <input
              type="file"
              accept="image/png,image/jpeg,image/webp,video/mp4"
              onChange={handleFileChange}
            />
          </label>
          {fileError && <div className="alert error" role="alert">{fileError}</div>}
          {selectedPreviewUrl && (
            <div className="screensaver-preview">
              <span>Seçilen {selectedIsVideo ? "video" : "görsel"} önizlemesi:</span>
              {selectedIsVideo ? (
                <video src={selectedPreviewUrl} controls muted />
              ) : (
                <img src={selectedPreviewUrl} alt="Seçilen ekran koruyucu görseli önizlemesi" />
              )}
            </div>
          )}
          {error && <div className="alert error" role="alert">{error}</div>}
          {statusMessage && <div className="alert information" role="status">{statusMessage}</div>}
          <button className="primary login-submit" disabled={busy || !selectedFile}>
            {busy ? "Yükleniyor…" : "Yükle"}
          </button>
        </form>
        {currentMediaUrl && (
          <div className="screensaver-preview screensaver-preview-current">
            <span>Az önce ayarlanan {currentMediaIsVideo ? "video" : "görsel"}:</span>
            {currentMediaIsVideo ? (
              <video src={currentMediaUrl} controls muted loop />
            ) : (
              <img src={currentMediaUrl} alt="Müşteri ekranında gösterilen mevcut ekran koruyucu görseli" />
            )}
            <button type="button" className="danger-text" disabled={busy} onClick={() => void remove()}>
              {busy ? "Kaldırılıyor…" : "Kaldır"}
            </button>
          </div>
        )}
        <p className="screensaver-settings-note">
          Bu ekran, sunucuda hâlihazırda ayarlı olanı otomatik göstermez —
          yalnızca bu oturumda yaptığınız son yükleme/kaldırma işlemini gösterir.
          Mevcut durumu görmek için müşteri ekranını boşta iken kontrol edin.
        </p>
      </section>
    </main>
  );
}
