import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
  type KeyboardEvent as ReactKeyboardEvent,
} from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { ApiError, api, registerSessionExpiredHandler } from "../api";
import type { CatalogProduct, DisplaySnapshot } from "../contracts";
import { isPlainClick, useRouter } from "../router";
import { navLabels, stateText } from "../strings";
import { formatMoney, formatQuantity } from "../format";
import { savedId } from "../storage";
import { ExperiencePage, type BackendStatus } from "./workspace";

type CashierSession = "checking" | "anonymous" | "ready";
type FeedbackKind = "error" | "success" | "conflict" | "unauthorized";

// V1-WTR-014: mirrors HelpRequestTypeCatalog
// (src/Host/Experience/HelpRequests/HelpRequestContracts.cs). The codes
// are the server's; only the wording is ours - same wording waiter-app.js
// uses for the same codes.
const helpRequestTypeLabels: Record<string, string> = {
  Spill: "Döküldü / temizlik gerekiyor",
  Complaint: "Misafir şikayeti",
  Approval: "Onay gerekiyor",
  Other: "Diğer",
};

interface HelpAlert {
  id: string;
  tableNumber: string;
  requestType: string;
  requestedByDisplayName: string;
}

const focusableSelector = [
  "button:not([disabled])",
  "input:not([disabled])",
  "[href]",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

// The customer display must load from its own origin so the browser partitions
// its storage from the cashier's (finding B-4). runtime-configuration provides
// that origin; without it the link stays relative (single-origin / legacy).
export function customerDisplayHref(originUrl: string): string {
  return `${originUrl.replace(/\/+$/, "")}/display`;
}

export function Cashier() {
  const { path: currentPath, navigate } = useRouter();
  const [terminalId] = useState(() => savedId("alkaros.terminal-id"));
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [capabilities, setCapabilities] = useState<string[]>([]);
  const [catalog, setCatalog] = useState<CatalogProduct[]>([]);
  const [order, setOrder] = useState<DisplaySnapshot | null>(null);
  const [pairingCode, setPairingCode] = useState("");
  const [customerDisplayUrl, setCustomerDisplayUrl] = useState("");
  const [reservationStationEnabled, setReservationStationEnabled] = useState(false);
  const [session, setSession] = useState<CashierSession>("checking");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  // V1-RMD-388 (Tur 2, T7): only set by the session-expired handler below,
  // never by restoreSession's own first-load 401 (that one is a normal
  // "not signed in yet", not a real mid-shift kick). A cashier who WAS
  // signed in and gets bounced here - by another manager revoking their
  // sessions from SecurityAdministration.tsx, or by the cookie simply
  // expiring - used to land on an unexplained login form with no clue why.
  const [sessionEndedMessage, setSessionEndedMessage] = useState("");
  const [notice, setNotice] = useState("");
  const [feedbackKind, setFeedbackKind] = useState<FeedbackKind>("success");
  const [search, setSearch] = useState("");
  const [activeCategory, setActiveCategory] = useState("ALL");
  const [backendStatus, setBackendStatus] = useState<BackendStatus>("checking");
  const [pairingOpen, setPairingOpen] = useState(false);
  const [helpAlerts, setHelpAlerts] = useState<HelpAlert[]>([]);
  // V1-RMD-286: 'ok' | 'reconnecting' (was connected, link dropped) | 'missed'
  // (link is back but calls sent while it was down cannot be recovered).
  const [helpLink, setHelpLink] = useState<"ok" | "reconnecting" | "missed">("ok");
  const pairingTrigger = useRef<HTMLButtonElement>(null);
  const pairingDialog = useRef<HTMLDivElement>(null);

  const reload = useCallback(async () => {
    const [session, products, active, config] = await Promise.all([
      api.session(terminalId),
      api.catalog(terminalId),
      api.activeOrder(terminalId),
      // Non-fatal: an operator can run without a kitchen station and still sell.
      api.runtimeConfig(terminalId).catch(() => null),
    ]);
    setDisplayName(session.displayName);
    setCapabilities(session.capabilities ?? []);
    setCatalog(products);
    setOrder(active);
    setCustomerDisplayUrl(config?.customerDisplayUrl ?? "");
    setReservationStationEnabled(config?.reservationStationEnabled ?? false);
    setSession("ready");
    setBackendStatus("online");
  }, [terminalId]);

  const restoreSession = useCallback(async () => {
    try {
      await reload();
    } catch (reason) {
      if (reason instanceof ApiError && reason.status === 401) setSession("anonymous");
      else {
        setSession("anonymous");
        setBackendStatus("offline");
      }
    }
  }, [reload]);

  useEffect(() => {
    void restoreSession();
  }, [restoreSession]);

  // V1-RMD-291: the one place that drops the session on a 401 from ANY call
  // in this app - not just the ones that already caught it locally
  // (restoreSession/execute/login below). Before this, a 401 from an
  // uncaught call (or a future one that forgets to check) left the cashier
  // stuck on a half-rendered screen with no way back to the login form.
  // A 401 from a rejected login itself also reaches here, but that is
  // harmless: the session is already anonymous, and login()'s own catch
  // still owns the Turkish message shown for it.
  useEffect(() => {
    registerSessionExpiredHandler(() => {
      setSession("anonymous");
      setSessionEndedMessage("Oturumunuz sonlandırıldı. Lütfen tekrar giriş yapın.");
    });
    return () => registerSessionExpiredHandler(null);
  }, []);

  // V1-WTR-014: a waiter's real-time call for help. The connection is
  // attempted for every signed-in session (matching CustomerDisplay's own
  // SignalR setup, this file's established precedent), not gated on
  // capabilities client-side - HelpRequestHub's own auth (the
  // manager/supervisor-only `alkaros.manager` cookie) is what actually
  // decides who receives anything; a cashier-only session's connection is
  // simply refused there, same cost as never attempting one.
  useEffect(() => {
    if (session !== "ready") return;
    // HubConnectionBuilder.build() itself resolves the URL against
    // window.location synchronously and can throw outright in an
    // environment without a real browser location (found by this file's
    // own test suite, not a real deployment) - defensive for the same
    // reason as api.ts's own network-failure handling: a real-time nicety
    // failing to initialize must never crash the cashier screen.
    // V1-RMD-286: the connection used to give up for good after five automatic
    // retries and a failed first start() was swallowed. Retry with a capped
    // exponential delay instead. A session the hub refuses (cashier-only, see
    // above) connects and is aborted at once, so it only counts as
    // 'established' - and only then may show a status - after staying up for
    // a few seconds; the backoff also only resets at that point.
    // There is no endpoint listing open help requests, so calls sent while
    // the link was down cannot be reloaded; the cashier is told they may
    // have been missed instead.
    const retryDelay = (attempt: number) => Math.min(15_000, 1_000 * 2 ** Math.min(attempt, 4));
    let connection: ReturnType<HubConnectionBuilder["build"]> | null = null;
    try {
      connection = new HubConnectionBuilder()
        .withUrl("/hubs/help-requests")
        .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (context) => retryDelay(context.previousRetryCount) })
        .configureLogging(LogLevel.Warning)
        .build();
    } catch {
      return;
    }
    const activeConnection = connection;
    let disposed = false;
    let established = false;
    let attempt = 0;
    let stableTimer: number | undefined;
    let retryTimer: number | undefined;
    const armStable = () => {
      window.clearTimeout(stableTimer);
      stableTimer = window.setTimeout(() => {
        established = true;
        attempt = 0;
        setHelpLink((current) => (current === "reconnecting" ? "missed" : current));
      }, 5_000);
    };
    const start = () => {
      activeConnection.start().then(armStable).catch(() => {
        if (disposed) return;
        retryTimer = window.setTimeout(start, retryDelay(attempt++));
      });
    };
    activeConnection.onreconnecting(() => {
      window.clearTimeout(stableTimer);
      if (established) setHelpLink("reconnecting");
    });
    activeConnection.onreconnected(() => {
      if (established) setHelpLink("missed");
      armStable();
    });
    // Not reachable while the reconnect policy above never gives up, and also
    // how a refused session ends; either way start again with backoff.
    activeConnection.onclose(() => {
      window.clearTimeout(stableTimer);
      if (disposed) return;
      if (established) setHelpLink("reconnecting");
      retryTimer = window.setTimeout(start, retryDelay(attempt++));
    });
    activeConnection.on(
      "HelpRequested",
      (payload: { tableId: string; tableNumber: string; requestType: string; requestedByDisplayName: string }) => {
        setHelpAlerts((previous) => [
          { id: `${payload.tableId}-${Date.now()}`, tableNumber: payload.tableNumber,
            requestType: payload.requestType, requestedByDisplayName: payload.requestedByDisplayName },
          ...previous,
        ]);
      },
    );
    start();
    return () => {
      disposed = true;
      window.clearTimeout(stableTimer);
      window.clearTimeout(retryTimer);
      void activeConnection.stop();
    };
  }, [session]);

  useEffect(() => {
    const check = () => {
      api.health()
        .then(() => setBackendStatus("online"))
        .catch(() => setBackendStatus("offline"));
    };
    check();
    const timer = window.setInterval(check, 10_000);
    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    if (!pairingOpen) return;
    const dialog = pairingDialog.current;
    const first = dialog?.querySelector<HTMLElement>(focusableSelector);
    first?.focus();
    return () => pairingTrigger.current?.focus();
  }, [pairingOpen]);

  const closePairing = () => setPairingOpen(false);

  const trapPairingFocus = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    if (event.key === "Escape") {
      event.preventDefault();
      closePairing();
      return;
    }
    if (event.key !== "Tab") return;
    const controls = [...event.currentTarget.querySelectorAll<HTMLElement>(focusableSelector)];
    if (controls.length === 0) return;
    const first = controls[0];
    const last = controls.at(-1)!;
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  const execute = async (action: () => Promise<unknown>, success?: string) => {
    setBusy(true);
    setError("");
    setNotice("");
    try {
      await action();
      await reload();
      if (success) {
        setFeedbackKind("success");
        setNotice(success);
      }
      return true;
    } catch (reason) {
      if (reason instanceof ApiError && reason.status === 401) {
        setSession("anonymous");
        setFeedbackKind("unauthorized");
      } else if (reason instanceof ApiError && reason.status === 409) {
        setFeedbackKind("conflict");
        await reload().catch(() => setBackendStatus("offline"));
      } else {
        setFeedbackKind("error");
      }
      // V1-RMD-114: an independent audit (2026-09-06) found this trusted
      // reason.message for ANY Error, not just ApiError — a raw network
      // failure (fetch() itself throwing, e.g. "Failed to fetch") is a
      // native, English, browser-generated message, not a server-mapped
      // Turkish one. Only ApiError's message is guaranteed to come from
      // the backend's own Turkish-mapped exception filter.
      setError(reason instanceof ApiError ? reason.message : "İşlem tamamlanamadı.");
      return false;
    } finally {
      setBusy(false);
    }
  };

  const login = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError("");
    setFeedbackKind("error");
    setSessionEndedMessage("");
    try {
      const result = await api.login(username, password, terminalId);
      setDisplayName(result.displayName);
      setCapabilities(result.capabilities ?? []);
      setPassword("");
      await reload();
    } catch (reason) {
      setSession("anonymous");
      setFeedbackKind(reason instanceof ApiError && reason.status === 401 ? "unauthorized" : "error");
      setError(reason instanceof ApiError ? reason.message : "Giriş yapılamadı.");
    } finally {
      setBusy(false);
    }
  };

  const logout = async () => {
    setBusy(true);
    setError("");
    try {
      await api.logout(terminalId);
      setSession("anonymous");
      setDisplayName("");
      setCapabilities([]);
      setCatalog([]);
      setOrder(null);
      setNotice("");
      // V1-RMD-314 (independent 2026-09-26 audit, finding K4): workspace.tsx reads/writes this key across
      // a page reload so a cashier can resume the split-payment screen they were on - but logout() never
      // cleared it, so a shift change let the NEXT cashier's session land straight on the PREVIOUS
      // cashier's open split-payment view for whatever bill happened to be last on this device.
      localStorage.removeItem("alkaros.current-bill-id");
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : "Oturum kapatılamadı.");
    } finally {
      setBusy(false);
    }
  };

  const categories = useMemo(() => {
    const values = new Map<string, string>();
    catalog.forEach((product) => values.set(product.categoryCode, product.categoryName));
    return [...values].map(([code, name]) => ({ code, name }));
  }, [catalog]);

  if (session !== "ready") {
    return (
      <main className="login-shell">
        <section className="login-intro" aria-label="ALKAROS kasa">
          <div className="brand-mark">A</div>
          <span className="eyebrow">ALKAROS RESTAURANT OS</span>
          <h1>Servis hızını<br />kasada koruyun.</h1>
          <p>Sipariş, müşteri ekranı ve mutfak akışı aynı gerçek veriyi takip eder.</p>
          <div className={`system-state ${backendStatus}`}>
            <span />
            {backendStatus === "online" ? "Kasa sunucusu hazır" :
              backendStatus === "offline" ? "Kasa sunucusuna ulaşılamıyor" : "Bağlantı kontrol ediliyor"}
          </div>
        </section>
        <form className="login-card" onSubmit={login} aria-busy={busy || session === "checking"}>
          <div className="login-card-heading">
            <span>Personel girişi</span>
            <h2>Kasayı aç</h2>
            <p>Yetkili kullanıcı hesabınızla devam edin.</p>
          </div>
          <div className={`system-state login-state ${backendStatus}`} role="status">
            <span />
            {backendStatus === "online" ? "Kasa sunucusu çevrimiçi" :
              backendStatus === "offline" ? "Kasa sunucusu çevrimdışı" : "Bağlantı kontrol ediliyor"}
          </div>
          <label>
            Kullanıcı adı
            <input
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              autoComplete="username"
              autoFocus
            />
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
          {session === "checking" && (
            <div className="alert information" role="status">Oturum ve bağlantı doğrulanıyor…</div>
          )}
          {sessionEndedMessage && <div className="alert error" role="alert">{sessionEndedMessage}</div>}
          {error && <div className={`alert ${feedbackKind}`} role="alert">{error}</div>}
          <button className="primary login-submit" disabled={busy || session === "checking" || backendStatus === "offline"}>
            {session === "checking" ? "Oturum kontrol ediliyor…" : busy ? "Giriş yapılıyor…" : "Giriş yap"}
          </button>
          <small className="terminal-reference">Terminal · {terminalId.slice(0, 8).toUpperCase()}</small>
        </form>
      </main>
    );
  }

  const activeOrder = order?.orderId ? order : null;
  const normalizedSearch = search.trim().toLocaleLowerCase("tr-TR");
  const visibleProducts = catalog.filter((product) =>
    (activeCategory === "ALL" || product.categoryCode === activeCategory)
    && (!normalizedSearch
      || product.name.toLocaleLowerCase("tr-TR").includes(normalizedSearch)
      || product.sku.toLocaleLowerCase("tr-TR").includes(normalizedSearch)));
  const itemCount = activeOrder?.lines.reduce((sum, line) => sum + line.quantity, 0) ?? 0;
  const canStartNextOrder = !activeOrder || !activeOrder.editable;

  if (currentPath !== "/") {
    return <ExperiencePage
      terminalId={terminalId}
      displayName={displayName || username}
      capabilities={capabilities}
      path={currentPath}
      backendStatus={backendStatus}
      customerDisplayUrl={customerDisplayUrl}
      onLogout={logout}
    />;
  }

  return (
    <main className="cashier-shell">
      <header className="pos-header">
        <div className="pos-brand">
          <div className="brand-mark compact">A</div>
          <div><strong>ALKAROS</strong><span>Kasa satış</span></div>
        </div>
        <div className="pos-header-actions">
          <div className={`system-state compact ${backendStatus}`} title="Gerçek /health/ready sonucu">
            <span />
            {backendStatus === "online" ? "Sunucu hazır" : "Bağlantı sorunu"}
          </div>
          <button
            className="header-action"
            onClick={() => window.open(customerDisplayHref(customerDisplayUrl), "alkaros-customer-display")}
          >
            Müşteri ekranı
          </button>
          {reservationStationEnabled && (
            <button
              className="header-action"
              onClick={() => window.open("/reservations", "alkaros-reservation-station")}
            >
              Rezervasyon istasyonu
            </button>
          )}
          {capabilities.includes("orders.create") && <a className="header-action" href="/pending-checks" onClick={(event) => { if (isPlainClick(event)) { event.preventDefault(); navigate("/pending-checks"); } }}>Bekleyen hesaplar</a>}
          {capabilities.includes("payments.take") && <a className="header-action" href="/cashier/payments/customer-accounts/index.html">Cari hesaplar</a>}
          <a className="header-action" href="/tables" onClick={(event) => { if (isPlainClick(event)) { event.preventDefault(); navigate("/tables"); } }}>Masalar</a>
          <a className="header-action" href="/kitchen" onClick={(event) => { if (isPlainClick(event)) { event.preventDefault(); navigate("/kitchen"); } }}>Mutfak</a>
          {capabilities.includes("catalog.manage") && <a className="header-action" href="/catalog" onClick={(event) => { if (isPlainClick(event)) { event.preventDefault(); navigate("/catalog"); } }}>Menü</a>}
          {capabilities.includes("reports.view") && <a className="header-action" href="/management" onClick={(event) => { if (isPlainClick(event)) { event.preventDefault(); navigate("/management"); } }}>{navLabels.management}</a>}
          <button
            className="header-action"
            ref={pairingTrigger}
            aria-haspopup="dialog"
            aria-expanded={pairingOpen}
            onClick={() => setPairingOpen(true)}
          >
            Ekranı eşleştir
          </button>
          <div className="operator-card"><span>{displayName || username}</span><small>Kasiyer</small></div>
          <button className="icon-action" disabled={busy} onClick={() => void logout()} aria-label="Oturumu kapat">
            Çıkış
          </button>
        </div>
      </header>

      {helpLink !== "ok" && (
        <div className="help-alerts" role="status" aria-live="polite">
          <div className="help-alert">
            <span>
              {helpLink === "reconnecting"
                ? "Yardım çağrısı bağlantısı koptu, yeniden bağlanılıyor."
                : "Bağlantı koptuğu sırada gelen yardım çağrıları görülmemiş olabilir."}
            </span>
            {helpLink === "missed" && (
              <button onClick={() => setHelpLink("ok")} aria-label={stateText.dismissMessage}>×</button>
            )}
          </div>
        </div>
      )}
      {helpAlerts.length > 0 && (
        <div className="help-alerts" role="alert" aria-live="assertive">
          {helpAlerts.map((alert) => (
            <div key={alert.id} className="help-alert">
              <span>
                <strong>{alert.tableNumber} masası</strong> — {helpRequestTypeLabels[alert.requestType] ?? alert.requestType}
                {" · "}{alert.requestedByDisplayName}
              </span>
              <button
                onClick={() => setHelpAlerts((previous) => previous.filter((candidate) => candidate.id !== alert.id))}
                aria-label={stateText.dismissMessage}
              >
                ×
              </button>
            </div>
          ))}
        </div>
      )}

      {(error || notice) && (
        <div
          className={`toast-message ${error ? feedbackKind : "success"}`}
          role={error ? "alert" : "status"}
          aria-live={error ? "assertive" : "polite"}
        >
          <span>{error || notice}</span>
          <button onClick={() => { setError(""); setNotice(""); }} aria-label={stateText.dismissMessage}>×</button>
        </div>
      )}

      <div className="mobile-ticket-bar" aria-label="Sipariş özeti">
        <div>
          <span>{activeOrder ? "Toplam" : "Aktif sipariş yok"}</span>
          <strong>{activeOrder ? formatMoney(activeOrder.total) : "Yeni satış"}</strong>
        </div>
        {!activeOrder ? (
          <button className="primary" disabled={busy} onClick={() => execute(() => api.startOrder(terminalId))}>
            Sipariş aç
          </button>
        ) : activeOrder.editable ? (
          <button
            className="primary"
            disabled={busy || activeOrder.lines.length === 0}
            onClick={() => execute(
              () => api.submitOrder(terminalId, activeOrder.orderId!, activeOrder.revision),
              "Sipariş gönderildi. Yeni satış açabilirsiniz.",
            )}
          >Gönder</button>
        ) : (
          <button className="secondary" disabled={busy} onClick={() => execute(() => api.startOrder(terminalId))}>
            Sonraki sipariş
          </button>
        )}
      </div>

      <div className="pos-workspace">
        {/* V1-RMD-364 (module-by-module UI audit): the same tab-pattern gap
            Cashier's vanilla client had (V1-RMD-360) - this filters which
            view of the SAME product grid shows, so role="tab" + aria-selected
            is the right pair (not radiogroup/radio, which Module 3's
            split-payment fix used for a VALUE choice instead). */}
        <nav className="category-rail" aria-label="Ürün kategorileri" role="tablist">
          <button
            className={activeCategory === "ALL" ? "active" : ""}
            role="tab"
            aria-selected={activeCategory === "ALL"}
            onClick={() => setActiveCategory("ALL")}
          >
            <span className="category-glyph">⌂</span>
            Tümü
            <small>{catalog.length}</small>
          </button>
          {categories.map((category, index) => (
            <button
              className={activeCategory === category.code ? "active" : ""}
              role="tab"
              aria-selected={activeCategory === category.code}
              key={category.code}
              onClick={() => setActiveCategory(category.code)}
            >
              <span className="category-glyph">{String(index + 1).padStart(2, "0")}</span>
              {category.name}
              <small>{catalog.filter((product) => product.categoryCode === category.code).length}</small>
            </button>
          ))}
        </nav>

        <section className="catalog-workspace">
          <div className="catalog-toolbar">
            <div>
              <span className="eyebrow dark">HIZLI SİPARİŞ</span>
              <h1>{activeCategory === "ALL" ? "Tüm ürünler" : categories.find((c) => c.code === activeCategory)?.name}</h1>
            </div>
            <label className="search-field">
              <span>⌕</span>
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Ürün veya SKU ara"
                aria-label="Ürün ara"
              />
            </label>
          </div>

          <div className="product-grid">
            {visibleProducts.map((product) => (
              <button
                className="product-card"
                key={product.productId}
                disabled={busy || !activeOrder?.editable}
                onClick={() => execute(() => api.addItem(
                  terminalId,
                  activeOrder!.orderId!,
                  product.productId,
                  activeOrder!.revision,
                ))}
              >
                <span className="product-category">{product.categoryName}</span>
                <strong>{product.name}</strong>
                <span className="product-card-footer">
                  <small>{product.sku}</small>
                  <span className="product-price"><b>{formatMoney(product.unitPrice)}</b><small>KDV dahil</small></span>
                </span>
              </button>
            ))}
            {visibleProducts.length === 0 && (
              <div className="catalog-empty">Aramanızla eşleşen aktif ürün bulunamadı.</div>
            )}
          </div>
        </section>

        <aside className="ticket-panel">
          <div className="ticket-heading">
            <div>
              <span className="eyebrow dark">AKTİF SİPARİŞ</span>
              <h2>{activeOrder?.orderNumber ?? "Yeni satış"}</h2>
            </div>
            <div className="ticket-meta"><strong>{formatQuantity(itemCount)}</strong><span>ürün</span></div>
          </div>

          {!activeOrder ? (
            <div className="new-order-state">
              <div className="new-order-icon">＋</div>
              <h3>Siparişe hazır</h3>
              <p>Ürünleri açmak ve yeni satışa başlamak için sipariş oluşturun.</p>
              <button className="primary large" disabled={busy} onClick={() => execute(() => api.startOrder(terminalId))}>
                Yeni sipariş aç
              </button>
            </div>
          ) : (
            <>
              <div className="ticket-lines" aria-live="polite" aria-atomic="false">
                {activeOrder.lines.length === 0 && (
                  <div className="ticket-empty">Soldaki katalogdan ürün seçin.</div>
                )}
                {activeOrder.lines.map((line) => (
                  <div className="ticket-line" key={line.itemId}>
                    <div className="ticket-line-main">
                      <strong>{line.name}</strong>
                      <small>{formatMoney(line.lineTotal / line.quantity)} × {formatQuantity(line.quantity)} · KDV dahil</small>
                    </div>
                    <strong className="line-total">{formatMoney(line.lineTotal)}</strong>
                    <div className="quantity-controls">
                      <button
                        aria-label={`${line.name} azalt`}
                        disabled={busy || line.quantity <= 1 || !activeOrder.editable}
                        onClick={() => execute(() => api.changeQuantity(
                          terminalId, activeOrder.orderId!, line.itemId, line.quantity - 1, activeOrder.revision,
                        ))}
                      >−</button>
                      <span>{formatQuantity(line.quantity)}</span>
                      <button
                        aria-label={`${line.name} artır`}
                        disabled={busy || !activeOrder.editable}
                        onClick={() => execute(() => api.changeQuantity(
                          terminalId, activeOrder.orderId!, line.itemId, line.quantity + 1, activeOrder.revision,
                        ))}
                      >+</button>
                      <button
                        className="remove"
                        disabled={busy || !activeOrder.editable}
                        onClick={() => execute(() => api.removeItem(
                          terminalId, activeOrder.orderId!, line.itemId, activeOrder.revision,
                        ))}
                      >Kaldır</button>
                    </div>
                  </div>
                ))}
              </div>
              <div className="ticket-totals">
                <div><span>Ara toplam</span><span>{formatMoney(activeOrder.subtotal)}</span></div>
                {activeOrder.discountTotal > 0 && (
                  <div><span>İndirim</span><span>−{formatMoney(activeOrder.discountTotal)}</span></div>
                )}
                <div><span>İçindeki KDV</span><span>{formatMoney(activeOrder.taxTotal)}</span></div>
                <div className="grand-total"><span>Toplam</span><strong>{formatMoney(activeOrder.total)}</strong></div>
              </div>
              {activeOrder.editable ? (
                <button
                  className="primary submit-order"
                  disabled={busy || activeOrder.lines.length === 0}
                  onClick={() => execute(
                    () => api.submitOrder(terminalId, activeOrder.orderId!, activeOrder.revision),
                    "Sipariş gönderildi. Yeni satış açabilirsiniz.",
                  )}
                >Siparişi gönder</button>
              ) : (
                <div className="submitted-state">
                  <span>✓</span>
                  <div><strong>Sipariş gönderildi</strong><small>Sürüm {activeOrder.revision}</small></div>
                </div>
              )}
              {canStartNextOrder && (
                <button className="secondary next-order" disabled={busy} onClick={() => execute(() => api.startOrder(terminalId))}>
                  Sonraki siparişi aç
                </button>
              )}
            </>
          )}

        </aside>
      </div>
      {pairingOpen && (
        <div className="dialog-backdrop" onMouseDown={(event) => {
          if (event.target === event.currentTarget) closePairing();
        }}>
          <div
            className="pairing-dialog"
            ref={pairingDialog}
            role="dialog"
            aria-modal="true"
            aria-labelledby="pairing-title"
            aria-describedby="pairing-description"
            onKeyDown={trapPairingFocus}
          >
            <div className="dialog-heading">
              <div>
                <span className="eyebrow dark">MÜŞTERİ EKRANI</span>
                <h2 id="pairing-title">Ekran bağlantısını yönetin</h2>
              </div>
              <button className="dialog-close" onClick={closePairing} aria-label="Eşleştirme penceresini kapat">×</button>
            </div>
            <p id="pairing-description">Müşteri ekranındaki 8 karakterli kodu girin veya mevcut ekran yetkisini kaldırın.</p>
            <label>
              Eşleştirme kodu
              <input
                value={pairingCode}
                onChange={(event) => setPairingCode(event.target.value.toUpperCase().replace(/[^A-Z0-9]/g, ""))}
                placeholder="Örn. A7K2M9P4"
                maxLength={8}
                autoComplete="off"
                inputMode="text"
              />
            </label>
            <div className="dialog-actions">
              <button className="secondary" onClick={closePairing}>Vazgeç</button>
              <button
                className="primary"
                disabled={busy || pairingCode.length !== 8}
                onClick={() => void execute(
                  () => api.approvePairing(terminalId, pairingCode),
                  "Müşteri ekranı eşleştirildi.",
                ).then((completed) => { if (completed) closePairing(); })}
              >{busy ? "Eşleştiriliyor…" : "Ekranı eşleştir"}</button>
            </div>
            <div className="dialog-danger-zone">
              <strong>Bağlantıyı sonlandır</strong>
              <span>Yetki kaldırıldığında müşteri ekranı yeniden kod ister.</span>
              <button
                className="danger-text"
                disabled={busy}
                onClick={() => void execute(
                  () => api.revokeDisplay(terminalId),
                  "Ekran yetkisi kaldırıldı.",
                ).then((completed) => { if (completed) closePairing(); })}
              >Aktif ekran yetkisini kaldır</button>
            </div>
          </div>
        </div>
      )}
    </main>
  );
}
