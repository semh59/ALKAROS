import { useCallback, useEffect, useId, useState, type FormEvent } from "react";
import {
  OnlinePlatformCredentialsApiError,
  describeChange,
  fieldLabel,
  loadOnlinePlatformCredentials,
  platformLabel,
  saveOnlinePlatformCredentials,
  type OnlinePlatformCredentials,
} from "./onlinePlatformCredentialsApi";
import "./online-platform-credentials.css";

const dateTime = (iso: string) =>
  new Date(iso).toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });

const failure = (error: unknown, fallback: string) => {
  if (!(error instanceof OnlinePlatformCredentialsApiError)) return fallback;
  return error.field ? `${fieldLabel(error.field)}: ${error.message}` : error.message;
};

/**
 * V12-OUI-003: a manager enters each online platform's API settings. A stored secret is never shown or sent
 * back: the form only says whether it is set; typing a new one replaces it, the remove button removes it. The
 * platform connections themselves are unverified drafts until each platform grants real access.
 */
export function OnlinePlatformCredentialsWorkspace({ terminalId }: { terminalId: string }) {
  const [platforms, setPlatforms] = useState<OnlinePlatformCredentials[] | null>(null);
  const [loadError, setLoadError] = useState<string>();

  const load = useCallback(async () => {
    try {
      setPlatforms(await loadOnlinePlatformCredentials(terminalId));
      setLoadError(undefined);
    } catch (error) {
      setLoadError(failure(error, "Platform bilgileri okunamadı."));
    }
  }, [terminalId]);

  useEffect(() => { void load(); }, [load]);

  const replace = (updated: OnlinePlatformCredentials) =>
    setPlatforms((current) => current?.map((platform) => (platform.provider === updated.provider ? updated : platform)) ?? null);

  return (
    <section className="platform-credentials" aria-labelledby="platform-credentials-title">
      <h2 id="platform-credentials-title" className="platform-credentials__title">Online platform bağlantı bilgileri</h2>
      <p className="platform-credentials__hint">
        Bilgiler şifreli saklanır; kayıtlı gizli anahtarlar bu ekranda bir daha gösterilmez. Platform bağlantıları
        henüz gerçek platform erişimiyle doğrulanmamış taslaklardır.
      </p>
      {loadError && <p className="platform-credentials__error" role="alert">{loadError}</p>}
      {platforms === null && !loadError && <p>Yükleniyor…</p>}
      {platforms?.map((platform) => (
        <PlatformForm key={platform.provider} terminalId={terminalId} platform={platform} onSaved={replace} />
      ))}
    </section>
  );
}

function PlatformForm({
  terminalId,
  platform,
  onSaved,
}: {
  terminalId: string;
  platform: OnlinePlatformCredentials;
  onSaved: (updated: OnlinePlatformCredentials) => void;
}) {
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [removals, setRemovals] = useState<ReadonlySet<string>>(new Set());
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const idPrefix = useId();
  const name = platformLabel(platform.provider);
  const change = describeChange(platform, drafts, removals);
  const hasChange = Object.keys(change.values).length > 0 || change.cleared.length > 0;

  const toggleRemoval = (field: string) =>
    setRemovals((current) => {
      const next = new Set(current);
      if (next.has(field)) next.delete(field);
      else next.add(field);
      return next;
    });

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!hasChange || busy) return;
    setBusy(true);
    setError(undefined);
    setNotice(undefined);
    try {
      const saved = await saveOnlinePlatformCredentials(terminalId, platform.provider, change);
      setDrafts({});
      setRemovals(new Set());
      onSaved(saved);
      setNotice(`${name} bilgileri kaydedildi.`);
    } catch (caught) {
      setError(failure(caught, `${name} bilgileri kaydedilemedi.`));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="platform-credentials__platform" aria-label={`${name} bağlantı bilgileri`} onSubmit={(event) => void submit(event)}>
      <h3>{name}</h3>
      <p className="platform-credentials__meta">
        {platform.updatedAt ? `Son değişiklik: ${dateTime(platform.updatedAt)}` : "Henüz bilgi kaydedilmedi."}
      </p>
      {platform.fields.map((field) => {
        const inputId = `${idPrefix}-${field.name}`;
        const removing = removals.has(field.name);
        return (
          <div key={field.name} className="platform-credentials__field">
            <label htmlFor={inputId}>{fieldLabel(field.name)}</label>
            <input
              id={inputId}
              type={field.isSecret ? "password" : "text"}
              autoComplete="off"
              spellCheck={false}
              disabled={removing || busy}
              value={drafts[field.name] ?? (field.isSecret ? "" : field.value ?? "")}
              placeholder={field.isSecret && field.configured ? "Değiştirmek için yeni değer girin" : undefined}
              onChange={(event) => { const value = event.target.value; setDrafts((current) => ({ ...current, [field.name]: value })); }}
            />
            <span className={`platform-credentials__state${removing ? " platform-credentials__state--removing" : ""}`}>
              {removing ? "Kaydedince silinecek" : field.configured ? "Kayıtlı" : "Kayıtlı değil"}
            </span>
            {field.configured && (
              <button
                type="button"
                className="platform-credentials__secondary"
                aria-pressed={removing}
                aria-label={`${fieldLabel(field.name)} alanını ${removing ? "silmekten vazgeç" : "kaldır"}`}
                disabled={busy}
                onClick={() => toggleRemoval(field.name)}
              >
                {removing ? "Vazgeç" : "Kaldır"}
              </button>
            )}
          </div>
        );
      })}
      <div className="platform-credentials__actions">
        <button type="submit" disabled={!hasChange || busy}>{busy ? "Kaydediliyor…" : "Kaydet"}</button>
      </div>
      {error && <p className="platform-credentials__error" role="alert">{error}</p>}
      <p className="platform-credentials__notice" role="status" aria-live="polite">{notice ?? ""}</p>
    </form>
  );
}
