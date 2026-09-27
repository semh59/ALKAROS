import { useCallback, useEffect, useState } from "react";
import { platformLabel } from "../online-operations";
import {
  OnlineStoreStatusApiError,
  busyChoices,
  deliveryText,
  loadStoreStatus,
  platformText,
  requestStoreStatus,
  requestedText,
  type OnlineStoreStatus,
  type StoreRequest,
} from "./onlineStoreStatusApi";
import "./online-store-status.css";

const REFRESH_MS = 30_000;

/**
 * V12-ONL-011: at the top of the online food settings, each platform with what the restaurant was set to, whether the
 * platform has been told yet, and what the platform itself reports; a manager opens it, pauses it as busy, or closes it
 * for the rest of the day. A closure ends by itself at its time.
 */
export function OnlineStoreStatusPanel({ terminalId }: { terminalId: string }) {
  const [rows, setRows] = useState<OnlineStoreStatus[] | null>(null);
  const [loadError, setLoadError] = useState<string>();
  const [actionError, setActionError] = useState<string>();
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      setRows(await loadStoreStatus(terminalId));
      setLoadError(undefined);
    } catch (error) {
      setLoadError(error instanceof OnlineStoreStatusApiError ? error.message : "Restoran durumu okunamadı.");
    }
  }, [terminalId]);

  useEffect(() => {
    void load();
    const timer = window.setInterval(() => void load(), REFRESH_MS);
    return () => window.clearInterval(timer);
  }, [load]);

  const change = async (provider: string, request: StoreRequest) => {
    setBusy(true);
    setActionError(undefined);
    try {
      await requestStoreStatus(terminalId, provider, request);
      await load();
    } catch (error) {
      setActionError(error instanceof OnlineStoreStatusApiError ? error.message : "Restoran durumu değiştirilemedi.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="online-store-status" aria-label="Restoran açık/kapalı">
      <h3 className="online-store-status__title">Restoran durumu</h3>
      {loadError && <p className="online-store-status__error" role="alert">{loadError}</p>}
      {actionError && <p className="online-store-status__error" role="alert">{actionError}</p>}
      {rows === null && !loadError && <p>Yükleniyor…</p>}
      <ul className="online-store-status__list">
        {rows?.map((row) => {
          const name = platformLabel(row.provider);
          const pending = deliveryText(row);
          return (
            <li key={row.provider} className="online-store-status__item">
              <p className="online-store-status__line">
                <strong>{name}</strong>
                <span>{row.configured ? requestedText(row) : "Bağlantı bilgileri eksik"}</span>
              </p>
              {row.configured && (
                <>
                  <p className="online-store-status__meta">{platformText(row)}</p>
                  {pending && (
                    <p className={`online-store-status__meta${row.delivery === "Retrying" ? " online-store-status__meta--warning" : ""}`} role="status">
                      {pending}
                    </p>
                  )}
                  <div className="online-store-status__buttons" role="group" aria-label={`${name} için restoran durumu`}>
                    <button type="button" disabled={busy || row.state === "Open"} onClick={() => void change(row.provider, { state: "Open" })}>
                      Aç
                    </button>
                    {busyChoices.map((minutes) => (
                      <button key={minutes} type="button" className="online-store-status__secondary" disabled={busy}
                        onClick={() => void change(row.provider, { state: "Busy", minutes })}>
                        {minutes} dk yoğun
                      </button>
                    ))}
                    <button type="button" className="online-store-status__secondary" disabled={busy || row.state === "ClosedToday"}
                      onClick={() => void change(row.provider, { state: "ClosedToday" })}>
                      Bugün kapat
                    </button>
                  </div>
                </>
              )}
            </li>
          );
        })}
      </ul>
    </section>
  );
}
