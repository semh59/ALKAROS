import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { ApiError, api } from "../api";
import type { PairingCreated } from "../contracts";
import { Icon, type IconName } from "../design-system";
import { afterFailure, afterSnapshot, displayPresentation, type DisplayFreshness } from "../stale";
import { formatMoney, formatQuantity } from "../format";
import { savedId } from "../storage";

type DisplaySession = "checking" | "pairing" | "ready" | "error";

export function CustomerDisplay() {
  const [displayId] = useState(() => savedId("alkaros.customer-display-id"));
  const [pairing, setPairing] = useState<PairingCreated | null>(null);
  const [pairingError, setPairingError] = useState("");
  const [displaySession, setDisplaySession] = useState<DisplaySession>("checking");
  const [freshness, setFreshness] = useState<DisplayFreshness>({
    snapshot: null,
    lastSuccessAt: null,
    connectionLost: false,
  });
  const [completedConcealed, setCompletedConcealed] = useState(false);
  const initialized = useRef(false);
  const pairingRequestInFlight = useRef(false);
  const hasSuccessfulSnapshot = useRef(false);

  const beginPairing = useCallback(async () => {
    if (pairingRequestInFlight.current) return;
    pairingRequestInFlight.current = true;
    setDisplaySession("pairing");
    setPairing(null);
    setPairingError("");
    try {
      setPairing(await api.createPairing(displayId));
    } catch (reason) {
      setPairingError(reason instanceof ApiError ? reason.message : "Eşleştirme başlatılamadı.");
      setDisplaySession("error");
    } finally {
      pairingRequestInFlight.current = false;
    }
  }, [displayId]);

  const refresh = useCallback(async () => {
    try {
      const snapshot = await api.snapshot(displayId);
      hasSuccessfulSnapshot.current = true;
      setFreshness(afterSnapshot(snapshot, Date.now()));
      setPairing(null);
      setPairingError("");
      setDisplaySession("ready");
    } catch (reason) {
      setFreshness((previous) => afterFailure(previous, Date.now()));
      if (reason instanceof ApiError && reason.status === 401) {
        await beginPairing();
      } else if (!hasSuccessfulSnapshot.current) {
        setPairingError(reason instanceof ApiError ? reason.message : "Ekran bilgisi alınamadı.");
        setDisplaySession("error");
      }
    }
  }, [beginPairing, displayId]);

  useEffect(() => {
    if (initialized.current) return;
    initialized.current = true;
    void refresh();
  }, [refresh]);

  useEffect(() => {
    if (!pairing || displaySession !== "pairing") return;
    const timer = window.setInterval(() => {
      api.completePairing(pairing.requestId, pairing.secret)
        .then(() => {
          setPairing(null);
          return refresh();
        })
        .catch((reason: unknown) => {
          if (reason instanceof ApiError && reason.status === 409) return;
          setPairing(null);
          setPairingError(reason instanceof ApiError ? reason.message : "Eşleştirme tamamlanamadı.");
          setDisplaySession("error");
        });
    }, 2_000);
    return () => window.clearInterval(timer);
  }, [displaySession, pairing, refresh]);

  useEffect(() => {
    if (!pairing) return;
    const expiresIn = Math.max(0, new Date(pairing.expiresAt).getTime() - Date.now());
    const timer = window.setTimeout(() => void beginPairing(), expiresIn);
    return () => window.clearTimeout(timer);
  }, [beginPairing, pairing]);

  useEffect(() => {
    if (displaySession !== "ready") return;
    const polling = window.setInterval(() => void refresh(), 5_000);
    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/customer-display")
      .withAutomaticReconnect([0, 1_000, 3_000, 5_000])
      .configureLogging(LogLevel.Warning)
      .build();
    connection.on("SnapshotChanged", () => void refresh());
    connection.onreconnecting(() => setFreshness((previous) => ({ ...previous, connectionLost: true })));
    void connection.start().catch(() => setFreshness((previous) => ({ ...previous, connectionLost: true })));
    return () => {
      window.clearInterval(polling);
      void connection.stop();
    };
  }, [displaySession, refresh]);

  useEffect(() => {
    if (!freshness.connectionLost || freshness.lastSuccessAt === null) return;
    const remaining = Math.max(0, 10_000 - (Date.now() - freshness.lastSuccessAt));
    const timer = window.setTimeout(
      () => setFreshness((previous) => afterFailure(previous, Date.now())),
      remaining,
    );
    return () => window.clearTimeout(timer);
  }, [freshness.connectionLost, freshness.lastSuccessAt]);

  useEffect(() => {
    const markOffline = () => setFreshness((previous) => afterFailure(previous, Date.now()));
    const markOnline = () => void refresh();
    window.addEventListener("offline", markOffline);
    window.addEventListener("online", markOnline);
    return () => {
      window.removeEventListener("offline", markOffline);
      window.removeEventListener("online", markOnline);
    };
  }, [refresh]);

  useEffect(() => {
    if (freshness.snapshot?.state !== "Completed") {
      setCompletedConcealed(false);
      return;
    }
    setCompletedConcealed(false);
    const timer = window.setTimeout(() => setCompletedConcealed(true), 7_000);
    return () => window.clearTimeout(timer);
  }, [freshness.snapshot?.revision, freshness.snapshot?.state]);

  if (displaySession === "checking") {
    return (
      <DisplayMessageScreen
        className="pairing-screen"
        kicker="BAĞLANTI KONTROLÜ"
        title="Müşteri ekranı hazırlanıyor"
        description="Güvenli ekran oturumu ve güncel sipariş bilgisi doğrulanıyor."
        busy
      />
    );
  }

  if (displaySession === "pairing") {
    return (
      <main className="display-shell pairing-screen">
        <DisplayBrand />
        <section className="display-center-card" aria-live="polite" aria-busy={!pairing}>
          <span className="display-kicker">GÜVENLİ EKRAN BAĞLANTISI</span>
          <h1>Müşteri ekranını<br />kasaya bağlayın</h1>
          {pairing ? (
            <>
              <p>Bu kodu kasa ekranındaki “Müşteri ekranı bağlantısı” alanına girin.</p>
              <div className="pairing-code">{pairing.code}</div>
              <small>Kod iki dakika sonra otomatik olarak yenilenir.</small>
            </>
          ) : <><span className="loading-spinner" aria-hidden="true" /><p>Güvenli eşleştirme kodu hazırlanıyor…</p></>}
        </section>
      </main>
    );
  }

  if (displaySession === "error") {
    return (
      <DisplayMessageScreen
        className="unavailable-screen"
        kicker="BAĞLANTI KURULAMADI"
        title="Ekran bilgisi alınamadı"
        description={pairingError || "Sunucuya ulaşılamadı. Bağlantıyı kontrol edip yeniden deneyin."}
        action={<button className="display-retry" onClick={() => void refresh()}>Tekrar dene</button>}
        alert
      />
    );
  }

  const snapshot = freshness.snapshot;
  if (!snapshot) {
    return <DisplayUnavailable onRetry={refresh} />;
  }

  const presentation = displayPresentation(snapshot);

  if (presentation === "unavailable") {
    return <DisplayUnavailable onRetry={refresh} />;
  }

  if (presentation === "completed" && !completedConcealed) {
    return (
      <DisplayMessageScreen
        className="completed-screen"
        kicker="SİPARİŞ TAMAMLANDI"
        title="Teşekkür ederiz."
        description={snapshot.message || "Siparişiniz tamamlandı. Afiyet olsun."}
        icon="check"
      />
    );
  }

  if (presentation === "idle" || (presentation === "completed" && completedConcealed)) {
    return (
      <main className="display-shell idle-screen">
        <DisplayBrand />
        {freshness.connectionLost && <ConnectionBanner />}
        <section className="display-center-card welcome-card">
          <span className="display-kicker">HOŞ GELDİNİZ</span>
          <h1>Siparişiniz için hazırız.</h1>
          <p>Kasiyer işlemi başlattığında ürün ve toplam bilgileri burada canlı olarak görünecek.</p>
          <div className="privacy-note">Bu ekran yalnız sipariş içeriğini gösterir.</div>
        </section>
      </main>
    );
  }

  const paying = presentation === "paying";
  const submitted = presentation === "active" && !snapshot.editable;
  return (
    <main className={`display-shell active-display ${paying ? "paying-display" : ""}`}>
      {freshness.connectionLost && <ConnectionBanner />}
      <header className="display-header">
        <DisplayBrand />
        <div className="display-order-number">
          <span>Sipariş</span>
          <strong>{snapshot.orderNumber}</strong>
        </div>
        <div className={`display-phase ${paying ? "paying" : submitted ? "submitted" : "active"}`} role="status">
          {paying ? "Ödeme işlemi sürüyor" : submitted ? "Sipariş kasadan gönderildi" : "Sipariş güncelleniyor"}
        </div>
      </header>
      <section className="display-order-grid">
        <div className="display-lines-card">
          <div className="display-section-title">
            <div><span className="display-kicker">SİPARİŞİNİZ</span><h1>Ürünler</h1></div>
            <strong>{formatQuantity(snapshot.lines.reduce((sum, line) => sum + line.quantity, 0))} adet</strong>
          </div>
          <div className="display-table-head"><span>Ürün</span><span>Adet</span><span>Tutar</span></div>
          <div className="display-lines-scroll">
            {snapshot.lines.map((line) => (
              <div className="display-line" key={line.itemId}>
                <div><strong>{line.name}</strong><small>{formatMoney(line.lineTotal / line.quantity, snapshot.currency)} · KDV dahil</small></div>
                <span className="display-quantity">{formatQuantity(line.quantity)}</span>
                <strong>{formatMoney(line.lineTotal, snapshot.currency)}</strong>
              </div>
            ))}
          </div>
        </div>
        <aside className="display-total-card">
          <span className="display-kicker">{paying ? "ÖDEMEDEKİ TUTAR" : "ÖDENECEK TOPLAM"}</span>
          <strong>{formatMoney(snapshot.total, snapshot.currency)}</strong>
          <div className="display-total-breakdown">
            <div><span>Ara toplam</span><span>{formatMoney(snapshot.subtotal, snapshot.currency)}</span></div>
            {snapshot.discountTotal > 0 && (
              <div><span>İndirim</span><span>−{formatMoney(snapshot.discountTotal, snapshot.currency)}</span></div>
            )}
            <div><span>KDV</span><span>{formatMoney(snapshot.taxTotal, snapshot.currency)}</span></div>
          </div>
          <div className="display-message"><span>{paying ? "…" : "✓"}</span><p>{snapshot.message}</p></div>
          <small>Canlı güncelleme · v{snapshot.revision}</small>
        </aside>
      </section>
    </main>
  );
}

function DisplayBrand() {
  return <div className="display-brand"><span>A</span><strong>ALKAROS</strong></div>;
}

function DisplayMessageScreen({
  className,
  kicker,
  title,
  description,
  action,
  icon,
  busy = false,
  alert = false,
}: {
  className: string;
  kicker: string;
  title: string;
  description: string;
  action?: ReactNode;
  icon?: IconName;
  busy?: boolean;
  alert?: boolean;
}) {
  return (
    <main className={`display-shell ${className}`}>
      <DisplayBrand />
      <section
        className="display-center-card"
        role={alert ? "alert" : "status"}
        aria-live={alert ? "assertive" : "polite"}
        aria-busy={busy}
      >
        {busy && <span className="loading-spinner" aria-hidden="true" />}
        {icon && <span className="completion-icon" aria-hidden="true"><Icon name={icon} /></span>}
        <span className="display-kicker">{kicker}</span>
        <h1>{title}</h1>
        <p>{description}</p>
        {action}
      </section>
    </main>
  );
}

function DisplayUnavailable({ onRetry }: { onRetry: () => Promise<void> }) {
  return (
    <DisplayMessageScreen
      className="unavailable-screen"
      kicker="GÜNCEL VERİ YOK"
      title="Bilgi güncellenemiyor"
      description="Eski ürün ve tutarlar güvenlik nedeniyle ekrandan kaldırıldı. Bağlantıyı kontrol edip yeniden deneyin."
      action={<button className="display-retry" onClick={() => void onRetry()}>Tekrar dene</button>}
      icon="warning"
      alert
    />
  );
}

function ConnectionBanner() {
  return <div className="connection-banner" role="status">Bağlantı yenileniyor · güncel olmayan tutarlar otomatik temizlenecek</div>;
}
