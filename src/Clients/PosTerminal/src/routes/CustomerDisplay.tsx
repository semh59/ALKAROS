import { useCallback, useEffect, useLayoutEffect, useRef, useState, type ReactNode } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { ApiError, api, type IdleScreensaver } from "../api";
import type { DisplayLine, DisplaySnapshot, PairingCreated } from "../contracts";
import { Icon, type IconName } from "../design-system";
import { afterFailure, afterSnapshot, displayPresentation, type DisplayFreshness } from "../stale";
import { formatMoney, formatQuantity } from "../format";
import { savedId } from "../storage";

type DisplaySession = "checking" | "pairing" | "ready" | "error";

// A pairing request the Host will never accept again: the display must ask for a fresh code on its own.
const endedPairingCodes = new Set(["PAIRING_EXPIRED", "PAIRING_CONSUMED", "PAIRING_LOCKED"]);

// What the code is worth if an older Host does not say how long it lives.
const defaultCodeSeconds = 120;
const codeSeconds = (created: PairingCreated) =>
  Number.isFinite(created.expiresInSeconds) && created.expiresInSeconds > 0 ? created.expiresInSeconds : defaultCodeSeconds;

// A 429 without Retry-After still pauses the display for a moment instead of being asked again at once.
const defaultRateLimitSeconds = 5;
const rateLimitWait = (reason: ApiError) => reason.retryAfterSeconds ?? defaultRateLimitSeconds;

const retryDelay = (attempt: number) => Math.min(15_000, 1_000 * 2 ** Math.min(attempt, 4));

// The display never prints the server's own wording: every failure is one of these fixed sentences.
function describeFailure(reason: unknown): { text: string; retryAfter?: number } {
  const status = reason instanceof ApiError ? reason.status : -1;
  if (status === 429) {
    return { text: "Kısa sürede çok fazla deneme yapıldı.", retryAfter: rateLimitWait(reason as ApiError) };
  }
  if (status === 0) return { text: "Sunucuya ulaşılamadı. Bağlantıyı kontrol edin." };
  if (status >= 500) return { text: "Sunucu şu an yanıt veremiyor." };
  return { text: "Ekran bilgisi alınamadı. Kasiyerden yardım isteyin." };
}

export function CustomerDisplay() {
  const [displayId] = useState(() => savedId("alkaros.customer-display-id"));
  const [pairing, setPairing] = useState<PairingCreated | null>(null);
  const [pairingMinutes, setPairingMinutes] = useState(1);
  const [pairingError, setPairingError] = useState("");
  const [retryIn, setRetryIn] = useState<number | null>(null);
  const [showCountdown, setShowCountdown] = useState(false);
  const retryAction = useRef<"pairing" | "snapshot">("snapshot");
  const failures = useRef(0);
  // A rate-limited answer pauses every request the display makes on its own until the server's Retry-After has passed.
  const pausedUntil = useRef(0);
  const [displaySession, setDisplaySession] = useState<DisplaySession>("checking");
  const [freshness, setFreshness] = useState<DisplayFreshness>({
    snapshot: null,
    lastSuccessAt: null,
    connectionLost: false,
  });
  const [completedConcealed, setCompletedConcealed] = useState(false);
  const [screensaver, setScreensaver] = useState<IdleScreensaver | null>(null);
  const initialized = useRef(false);
  const pairingRequestInFlight = useRef(false);
  const hasSuccessfulSnapshot = useRef(false);
  const requestSequence = useRef(0);
  const appliedSequence = useRef(0);

  const failWith = useCallback((reason: unknown, action: "pairing" | "snapshot") => {
    const failure = describeFailure(reason);
    if (failure.retryAfter !== undefined) pausedUntil.current = Date.now() + failure.retryAfter * 1_000;
    failures.current += 1;
    retryAction.current = action;
    setPairingError(failure.text);
    setShowCountdown(failure.retryAfter !== undefined);
    setRetryIn(failure.retryAfter ?? retryDelay(failures.current - 1) / 1_000);
    setDisplaySession("error");
  }, []);

  const beginPairing = useCallback(async () => {
    if (pairingRequestInFlight.current) return;
    pairingRequestInFlight.current = true;
    setDisplaySession("pairing");
    setPairing(null);
    setPairingError("");
    try {
      const created = await api.createPairing(displayId);
      failures.current = 0;
      setPairingMinutes(Math.max(1, Math.round(codeSeconds(created) / 60)));
      setPairing(created);
    } catch (reason) {
      failWith(reason, "pairing");
    } finally {
      pairingRequestInFlight.current = false;
    }
  }, [displayId, failWith]);

  const refresh = useCallback(async () => {
    // Polling, the hub and the online event can overlap; an answer older than one already shown is dropped.
    const sequence = ++requestSequence.current;
    setRetryIn(null);
    try {
      const snapshot = await api.snapshot(displayId);
      if (sequence < appliedSequence.current) return;
      appliedSequence.current = sequence;
      hasSuccessfulSnapshot.current = true;
      failures.current = 0;
      setFreshness(afterSnapshot(snapshot, Date.now()));
      setPairing(null);
      setPairingError("");
      setDisplaySession("ready");
    } catch (reason) {
      if (sequence < appliedSequence.current) return;
      setFreshness((previous) => afterFailure(previous, Date.now()));
      if (reason instanceof ApiError && reason.status === 401) {
        await beginPairing();
      } else if (!hasSuccessfulSnapshot.current) {
        failWith(reason, "snapshot");
      } else if (reason instanceof ApiError && reason.status === 429) {
        pausedUntil.current = Date.now() + rateLimitWait(reason) * 1_000;
      }
    }
  }, [beginPairing, displayId, failWith]);

  // Anything the display starts on its own (polling, hub, coming back online) waits out a rate limit; only a person's tap does not.
  const refreshUnlessPaused = useCallback(() => {
    if (Date.now() >= pausedUntil.current) void refresh();
  }, [refresh]);

  // A network drop, a 5xx or a 429 used to leave the error screen up until someone touched it; the screen now tries
  // again by itself, after the server's Retry-After or a backoff of up to 15 seconds, counting the seconds down.
  useEffect(() => {
    if (displaySession !== "error" || retryIn === null) return;
    if (retryIn <= 0) {
      setRetryIn(null);
      void (retryAction.current === "pairing" ? beginPairing() : refresh());
      return;
    }
    const timer = window.setTimeout(() => setRetryIn(retryIn - 1), 1_000);
    return () => window.clearTimeout(timer);
  }, [beginPairing, displaySession, refresh, retryIn]);

  useEffect(() => {
    if (initialized.current) return;
    initialized.current = true;
    void refresh();
  }, [refresh]);

  useEffect(() => {
    if (!pairing || displaySession !== "pairing") return;
    // A failure that arrives after the code was replaced must not start yet another code; a success is always real.
    let active = true;
    const timer = window.setInterval(() => {
      if (Date.now() < pausedUntil.current) return;
      api.completePairing(pairing.requestId, pairing.secret)
        .then(() => refresh())
        .catch((reason: unknown) => {
          if (!active) return;
          if (reason instanceof ApiError && reason.status === 429) {
            pausedUntil.current = Date.now() + rateLimitWait(reason) * 1_000;
            return;
          }
          // Not approved yet, a dropped connection or a 5xx: keep polling the same request.
          if (!(reason instanceof ApiError) || reason.status === 409 || reason.status >= 500 || reason.status === 0) return;
          // The request is over (expired, used, locked, unknown): ask for a new code instead of an error screen.
          if (endedPairingCodes.has(reason.code) || reason.status === 404 || reason.status === 401) {
            void beginPairing();
            return;
          }
          setPairing(null);
          failWith(reason, "pairing");
        });
    }, 2_000);
    return () => {
      active = false;
      window.clearInterval(timer);
    };
  }, [beginPairing, displaySession, failWith, pairing, refresh]);

  useEffect(() => {
    if (!pairing) return;
    const timer = window.setTimeout(() => void beginPairing(), codeSeconds(pairing) * 1_000);
    return () => window.clearTimeout(timer);
  }, [beginPairing, pairing]);

  useEffect(() => {
    if (displaySession !== "ready") return;
    const polling = window.setInterval(refreshUnlessPaused, 5_000);
    // The reconnect policy never gives up, and a connection that still ends (or never starts) is started
    // again with the same backoff, so live updates return without a page reload.
    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/customer-display")
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (context) => retryDelay(context.previousRetryCount) })
      .configureLogging(LogLevel.Warning)
      .build();
    let disposed = false;
    let attempt = 0;
    let restartTimer: number | undefined;
    const start = () => {
      connection.start().then(() => { attempt = 0; }).catch(() => {
        if (disposed) return;
        setFreshness((previous) => ({ ...previous, connectionLost: true }));
        restartTimer = window.setTimeout(start, retryDelay(attempt++));
      });
    };
    connection.on("SnapshotChanged", refreshUnlessPaused);
    connection.onreconnecting(() => setFreshness((previous) => ({ ...previous, connectionLost: true })));
    connection.onreconnected(refreshUnlessPaused);
    connection.onclose(() => {
      if (disposed) return;
      setFreshness((previous) => ({ ...previous, connectionLost: true }));
      restartTimer = window.setTimeout(start, retryDelay(attempt++));
    });
    start();
    return () => {
      disposed = true;
      window.clearInterval(polling);
      window.clearTimeout(restartTimer);
      void connection.stop();
    };
  }, [displaySession, refreshUnlessPaused]);

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
    const markOnline = refreshUnlessPaused;
    window.addEventListener("offline", markOffline);
    window.addEventListener("online", markOnline);
    return () => {
      window.removeEventListener("offline", markOffline);
      window.removeEventListener("online", markOnline);
    };
  }, [refreshUnlessPaused]);

  useEffect(() => {
    if (freshness.snapshot?.state !== "Completed") {
      setCompletedConcealed(false);
      return;
    }
    setCompletedConcealed(false);
    const timer = window.setTimeout(() => setCompletedConcealed(true), 7_000);
    return () => window.clearTimeout(timer);
  }, [freshness.snapshot?.revision, freshness.snapshot?.state]);

  const idleShown = freshness.snapshot
    ? displayPresentation(freshness.snapshot) === "idle"
      || (displayPresentation(freshness.snapshot) === "completed" && completedConcealed)
    : false;

  // V1-CDP-002: refetches on every fresh entry into Idle (dependency flips
  // false -> true), not continuously while idle stays shown — a business's
  // screensaver has no live-update requirement (out of scope, see the
  // task's own note), just "don't show a stale one from an earlier visit".
  useEffect(() => {
    if (!idleShown) {
      setScreensaver(null);
      return;
    }
    let cancelled = false;
    let objectUrl: string | null = null;
    void api.fetchIdleScreensaver(displayId).then((result) => {
      if (cancelled) {
        if (result) URL.revokeObjectURL(result.url);
        return;
      }
      objectUrl = result?.url ?? null;
      setScreensaver(result);
    });
    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [idleShown, displayId]);

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
              <p>Bu kodu kasadaki “Ekranı eşleştir” düğmesiyle açılan pencereye girin.</p>
              <div className="pairing-code">{pairing.code}</div>
              <small>Kod {pairingMinutes} dakika sonra otomatik olarak yenilenir.</small>
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
        description={`${pairingError || "Sunucuya ulaşılamadı. Bağlantıyı kontrol edin."}${
          showCountdown && retryIn !== null ? ` ${retryIn} saniye sonra kendiliğinden yeniden denenecek.` : ""}`}
        action={<button data-telemetry-id="b.routes.customerdisplay.1" className="display-retry" onClick={() => void (retryAction.current === "pairing" ? beginPairing() : refresh())}>Tekrar dene</button>}
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
      >
        {snapshot.paidTotal > 0 && (
          <div className="display-paid-total"><span>Ödenen toplam</span><strong>{formatMoney(snapshot.paidTotal, snapshot.currency)}</strong></div>
        )}
      </DisplayMessageScreen>
    );
  }

  if (presentation === "idle" || (presentation === "completed" && completedConcealed)) {
    if (screensaver) {
      const isVideo = screensaver.contentType.startsWith("video/");
      return (
        <main className="display-shell idle-screen idle-screen-with-image">
          {isVideo ? (
            <video
              className="idle-screensaver-image"
              src={screensaver.url}
              autoPlay
              muted
              loop
              playsInline
            />
          ) : (
            <img src={screensaver.url} alt="" className="idle-screensaver-image" />
          )}
          <DisplayBrand />
          {freshness.connectionLost && <ConnectionBanner />}
          <div className="privacy-note idle-screensaver-note">Bu ekran yalnız sipariş içeriğini gösterir.</div>
        </main>
      );
    }
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

  const submitted = presentation === "active" && !snapshot.editable;
  const partlyPaid = snapshot.paidTotal > 0 && typeof snapshot.remainingTotal === "number";
  return (
    <main className="display-shell active-display">
      {freshness.connectionLost && <ConnectionBanner />}
      <header className="display-header">
        <DisplayBrand />
        <div className="display-order-number">
          <span>Sipariş</span>
          <strong>{snapshot.orderNumber}</strong>
        </div>
        <div className={`display-phase ${submitted ? "submitted" : "active"}`} role="status">
          {submitted ? "Sipariş kasadan gönderildi" : "Sipariş güncelleniyor"}
        </div>
      </header>
      <section className="display-order-grid">
        <div className="display-lines-card">
          <div className="display-section-title">
            <div><span className="display-kicker">SİPARİŞİNİZ</span><h1>Ürünler</h1></div>
            <strong>{formatQuantity(snapshot.lines.reduce((sum, line) => sum + line.quantity, 0))} adet</strong>
          </div>
          <div className="display-table-head"><span>Ürün</span><span>Adet</span><span>Tutar</span></div>
          <DisplayLines lines={snapshot.lines} currency={snapshot.currency} />
        </div>
        <aside className="display-total-card">
          <span className="display-kicker">{partlyPaid ? "KALAN TUTAR" : "ÖDENECEK TOPLAM"}</span>
          <strong className="display-total-figure">{formatMoney(typeof snapshot.remainingTotal === "number" ? snapshot.remainingTotal : snapshot.total, snapshot.currency)}</strong>
          <DisplayStatement snapshot={snapshot} partlyPaid={partlyPaid} />
          <div className="display-message"><span>✓</span><p>{snapshot.message}</p></div>
        </aside>
      </section>
    </main>
  );
}

// The customer reads the bill top to bottom: what was ordered, what was taken off, what was added, what is owed, what is paid.
function DisplayStatement({ snapshot, partlyPaid }: { snapshot: DisplaySnapshot; partlyPaid: boolean }) {
  const money = (value: number) => formatMoney(value, snapshot.currency);
  const adjusted = snapshot.discountTotal > 0 || snapshot.tipTotal > 0 || snapshot.feeTotal > 0 || partlyPaid;
  return (
    <div className="display-total-breakdown">
      <div><span>Ara toplam</span><span>{money(snapshot.subtotal)}</span></div>
      {snapshot.discountTotal > 0 && <div><span>İndirim</span><span>−{money(snapshot.discountTotal)}</span></div>}
      {snapshot.tipTotal > 0 && <div><span>Bahşiş</span><span>+{money(snapshot.tipTotal)}</span></div>}
      {snapshot.feeTotal > 0 && <div><span>Ücret</span><span>+{money(snapshot.feeTotal)}</span></div>}
      {adjusted && <div><span>Hesap toplamı</span><span>{money(snapshot.total)}</span></div>}
      {partlyPaid && <div><span>Ödenen</span><span>−{money(snapshot.paidTotal)}</span></div>}
      <div><span>İçindeki KDV</span><span>{money(snapshot.taxTotal)}</span></div>
    </div>
  );
}

const pageSeconds = 6;

// A long order cannot all be seen at once: the list turns its own pages, and says how many items are still below.
function DisplayLines({ lines, currency }: { lines: DisplayLine[]; currency: string }) {
  const list = useRef<HTMLDivElement>(null);
  const [below, setBelow] = useState(0);

  const measure = useCallback(() => {
    const element = list.current;
    if (!element) return;
    const bottom = element.scrollTop + element.clientHeight;
    setBelow(Array.from(element.children).filter((row) => (row as HTMLElement).offsetTop + (row as HTMLElement).offsetHeight > bottom + 1).length);
  }, []);

  useLayoutEffect(measure, [lines, measure]);

  useEffect(() => {
    // The box changes size with the window, the connection notice and web fonts, not only when it scrolls.
    const observer = new ResizeObserver(measure);
    observer.observe(list.current!);
    const timer = window.setInterval(() => {
      const element = list.current;
      if (!element || element.scrollHeight <= element.clientHeight) return;
      const end = element.scrollHeight - element.clientHeight;
      // The next page starts at the first item that was cut off, so no item is ever shown half.
      const firstCutOff = Array.from(element.children).find(
        (row) => (row as HTMLElement).offsetTop + (row as HTMLElement).offsetHeight > element.scrollTop + element.clientHeight + 1,
      ) as HTMLElement | undefined;
      // An item taller than the box cannot be started again, so the page moves on by a box height instead.
      const next = firstCutOff && firstCutOff.offsetTop > element.scrollTop ? firstCutOff.offsetTop : element.scrollTop + element.clientHeight;
      const top = element.scrollTop >= end - 1 ? 0 : Math.min(end, next);
      const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
      element.scrollTo({ top, behavior: reduceMotion ? "auto" : "smooth" });
    }, pageSeconds * 1_000);
    return () => {
      observer.disconnect();
      window.clearInterval(timer);
    };
  }, [measure]);

  return (
    <>
      <div className="display-lines-scroll" ref={list} onScroll={measure}>
        {lines.map((line) => (
          <div className="display-line" key={line.itemId}>
            <div>
              <strong>{line.name}</strong>
              <small>{formatMoney(line.lineTotal / line.quantity, currency)} · KDV dahil</small>
              {line.discountAmount > 0 && (
                <em className="display-line-note">
                  {line.discountAmount >= line.lineTotal ? "İkram" : `İndirim −${formatMoney(line.discountAmount, currency)}`}
                </em>
              )}
            </div>
            <span className="display-quantity">{formatQuantity(line.quantity)}</span>
            <div className="display-line-amount">
              <strong>{formatMoney(line.lineTotal - line.discountAmount, currency)}</strong>
              {line.discountAmount > 0 && <s>{formatMoney(line.lineTotal, currency)}</s>}
            </div>
          </div>
        ))}
      </div>
      {below > 0 && <div className="display-more-lines" role="status">+{below} kalem daha</div>}
    </>
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
  children,
}: {
  className: string;
  kicker: string;
  title: string;
  description: string;
  action?: ReactNode;
  icon?: IconName;
  busy?: boolean;
  alert?: boolean;
  children?: ReactNode;
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
        {children}
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
      action={<button data-telemetry-id="b.routes.customerdisplay.2" className="display-retry" onClick={() => void onRetry()}>Tekrar dene</button>}
      icon="warning"
      alert
    />
  );
}

function ConnectionBanner() {
  return <div className="connection-banner" role="status">Bağlantı yenileniyor · güncel olmayan tutarlar otomatik temizlenecek</div>;
}
