import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { ApiError, api } from "./api";
import type { CatalogProduct, DisplaySnapshot, PairingCreated } from "./contracts";
import { afterFailure, afterSnapshot, type DisplayFreshness } from "./stale";

const formatMoney = (value: number, currency = "TRY") =>
  new Intl.NumberFormat("tr-TR", { style: "currency", currency }).format(value);

const formatQuantity = (value: number) =>
  new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 2 }).format(value);

const grossUnitPrice = (netUnitPrice: number, taxRate: number) =>
  Math.round((netUnitPrice * (1 + taxRate / 100) + Number.EPSILON) * 100) / 100;

function savedId(key: string): string {
  const existing = localStorage.getItem(key);
  if (existing) return existing;
  const created = crypto.randomUUID();
  localStorage.setItem(key, created);
  return created;
}

export function App() {
  return window.location.pathname.startsWith("/display") ? <CustomerDisplay /> : <Cashier />;
}

type BackendStatus = "checking" | "online" | "offline";

function Cashier() {
  const [terminalId] = useState(() => savedId("alkaros.terminal-id"));
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [catalog, setCatalog] = useState<CatalogProduct[]>([]);
  const [order, setOrder] = useState<DisplaySnapshot | null>(null);
  const [pairingCode, setPairingCode] = useState("");
  const [authenticated, setAuthenticated] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [search, setSearch] = useState("");
  const [activeCategory, setActiveCategory] = useState("ALL");
  const [backendStatus, setBackendStatus] = useState<BackendStatus>("checking");

  const reload = useCallback(async () => {
    const [session, products, active] = await Promise.all([
      api.session(terminalId),
      api.catalog(terminalId),
      api.activeOrder(terminalId),
    ]);
    setDisplayName(session.displayName);
    setCatalog(products);
    setOrder(active);
    setAuthenticated(true);
    setBackendStatus("online");
  }, [terminalId]);

  const restoreSession = useCallback(async () => {
    try {
      await reload();
    } catch (reason) {
      if (reason instanceof ApiError && reason.status === 401) setAuthenticated(false);
      else setBackendStatus("offline");
    }
  }, [reload]);

  useEffect(() => {
    void restoreSession();
  }, [restoreSession]);

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

  const execute = async (action: () => Promise<unknown>, success?: string) => {
    setBusy(true);
    setError("");
    setNotice("");
    try {
      await action();
      await reload();
      if (success) setNotice(success);
    } catch (reason) {
      if (reason instanceof ApiError && reason.status === 401) setAuthenticated(false);
      setError(reason instanceof Error ? reason.message : "İşlem tamamlanamadı.");
    } finally {
      setBusy(false);
    }
  };

  const login = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const result = await api.login(username, password, terminalId);
      setDisplayName(result.displayName);
      setPassword("");
      await reload();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Giriş yapılamadı.");
    } finally {
      setBusy(false);
    }
  };

  const logout = async () => {
    setBusy(true);
    setError("");
    try {
      await api.logout(terminalId);
      setAuthenticated(false);
      setDisplayName("");
      setCatalog([]);
      setOrder(null);
      setNotice("");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Oturum kapatılamadı.");
    } finally {
      setBusy(false);
    }
  };

  const categories = useMemo(() => {
    const values = new Map<string, string>();
    catalog.forEach((product) => values.set(product.categoryCode, product.categoryName));
    return [...values].map(([code, name]) => ({ code, name }));
  }, [catalog]);

  if (!authenticated) {
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
        <form className="login-card" onSubmit={login}>
          <div className="login-card-heading">
            <span>Personel girişi</span>
            <h2>Kasayı aç</h2>
            <p>Yetkili kullanıcı hesabınızla devam edin.</p>
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
          {error && <div className="alert error" role="alert">{error}</div>}
          <button className="primary login-submit" disabled={busy || backendStatus === "offline"}>
            {busy ? "Giriş yapılıyor…" : "Giriş yap"}
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
          <button className="header-action" onClick={() => window.open("/display", "alkaros-customer-display")}>
            Müşteri ekranı
          </button>
          <div className="operator-card"><span>{displayName || username}</span><small>Kasiyer</small></div>
          <button className="icon-action" disabled={busy} onClick={() => void logout()} aria-label="Oturumu kapat">
            Çıkış
          </button>
        </div>
      </header>

      {(error || notice) && (
        <div className={`toast-message ${error ? "error" : "success"}`} role="status">
          <span>{error || notice}</span>
          <button onClick={() => { setError(""); setNotice(""); }} aria-label="Mesajı kapat">×</button>
        </div>
      )}

      <div className="pos-workspace">
        <nav className="category-rail" aria-label="Ürün kategorileri">
          <button
            className={activeCategory === "ALL" ? "active" : ""}
            onClick={() => setActiveCategory("ALL")}
          >
            <span className="category-glyph">⌂</span>
            Tümü
            <small>{catalog.length}</small>
          </button>
          {categories.map((category, index) => (
            <button
              className={activeCategory === category.code ? "active" : ""}
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
                  <span className="product-price"><b>{formatMoney(grossUnitPrice(product.unitPrice, product.taxRate))}</b><small>KDV dahil</small></span>
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
              <div className="ticket-lines">
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
                <div><span>KDV</span><span>{formatMoney(activeOrder.taxTotal)}</span></div>
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
                  <div><strong>Sipariş gönderildi</strong><small>Revision {activeOrder.revision}</small></div>
                </div>
              )}
              {canStartNextOrder && (
                <button className="secondary next-order" disabled={busy} onClick={() => execute(() => api.startOrder(terminalId))}>
                  Sonraki siparişi aç
                </button>
              )}
            </>
          )}

          <details className="pairing-card">
            <summary><span>Müşteri ekranı bağlantısı</span><small>Kurulum ve yetki</small></summary>
            <div className="pairing-content">
              <label>
                Ekrandaki 8 karakterli kod
                <div className="pairing-row">
                  <input
                    value={pairingCode}
                    onChange={(event) => setPairingCode(event.target.value.toUpperCase())}
                    placeholder="Örn. A7K2M9P4"
                    maxLength={8}
                  />
                  <button
                    className="secondary"
                    disabled={busy || pairingCode.length !== 8}
                    onClick={() => execute(
                      () => api.approvePairing(terminalId, pairingCode),
                      "Müşteri ekranı eşleştirildi.",
                    )}
                  >Eşleştir</button>
                </div>
              </label>
              <button
                className="danger-text"
                disabled={busy}
                onClick={() => execute(() => api.revokeDisplay(terminalId), "Ekran yetkisi kaldırıldı.")}
              >Aktif ekran yetkisini kaldır</button>
            </div>
          </details>
        </aside>
      </div>
    </main>
  );
}

function CustomerDisplay() {
  const [displayId] = useState(() => savedId("alkaros.customer-display-id"));
  const [pairing, setPairing] = useState<PairingCreated | null>(null);
  const [pairingError, setPairingError] = useState("");
  const [freshness, setFreshness] = useState<DisplayFreshness>({
    snapshot: null,
    lastSuccessAt: null,
    connectionLost: false,
  });
  const [paired, setPaired] = useState(false);
  const initialized = useRef(false);
  const pairingRequestInFlight = useRef(false);

  const beginPairing = useCallback(async () => {
    if (pairingRequestInFlight.current) return;
    pairingRequestInFlight.current = true;
    setPairingError("");
    try {
      setPairing(await api.createPairing(displayId));
    } catch (reason) {
      setPairingError(reason instanceof Error ? reason.message : "Eşleştirme başlatılamadı.");
    } finally {
      pairingRequestInFlight.current = false;
    }
  }, [displayId]);

  const refresh = useCallback(async () => {
    try {
      const snapshot = await api.snapshot(displayId);
      setFreshness(afterSnapshot(snapshot, Date.now()));
      setPaired(true);
    } catch (reason) {
      setFreshness((previous) => afterFailure(previous, Date.now()));
      if (reason instanceof ApiError && reason.status === 401) {
        setPaired(false);
        await beginPairing();
      }
    }
  }, [beginPairing, displayId]);

  useEffect(() => {
    if (initialized.current) return;
    initialized.current = true;
    void refresh();
  }, [refresh]);

  useEffect(() => {
    if (!pairing || paired) return;
    const timer = window.setInterval(() => {
      api.completePairing(pairing.requestId, pairing.secret)
        .then(() => {
          setPaired(true);
          setPairing(null);
          return refresh();
        })
        .catch((reason: unknown) => {
          if (reason instanceof ApiError && reason.status === 409) return;
          setFreshness((previous) => afterFailure(previous, Date.now()));
        });
    }, 2_000);
    return () => window.clearInterval(timer);
  }, [paired, pairing, refresh]);

  useEffect(() => {
    if (!paired) return;
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
  }, [paired, refresh]);

  if (!paired) {
    return (
      <main className="display-shell pairing-screen">
        <DisplayBrand />
        <section className="display-center-card">
          <span className="display-kicker">GÜVENLİ EKRAN BAĞLANTISI</span>
          <h1>Müşteri ekranını<br />kasaya bağlayın</h1>
          {pairing ? (
            <>
              <p>Bu kodu kasa ekranındaki “Müşteri ekranı bağlantısı” alanına girin.</p>
              <div className="pairing-code">{pairing.code}</div>
              <small>Kod iki dakika sonra otomatik olarak yenilenir.</small>
            </>
          ) : pairingError ? (
            <>
              <div className="display-error">{pairingError}</div>
              <button className="display-retry" onClick={() => void beginPairing()}>Tekrar dene</button>
            </>
          ) : <p>Güvenli eşleştirme kodu hazırlanıyor…</p>}
        </section>
      </main>
    );
  }

  const snapshot = freshness.snapshot;
  if (!snapshot) {
    return (
      <main className="display-shell unavailable-screen">
        <DisplayBrand />
        <section className="display-center-card">
          <span className="connection-icon">!</span>
          <h1>Bilgi güncellenemiyor</h1>
          <p>Eski tutarlar güvenlik nedeniyle ekrandan kaldırıldı. Lütfen kasiyeri takip edin.</p>
        </section>
      </main>
    );
  }

  if (snapshot.state === "Idle") {
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

  return (
    <main className="display-shell active-display">
      {freshness.connectionLost && <ConnectionBanner />}
      <header className="display-header">
        <DisplayBrand />
        <div className="display-order-number">
          <span>Sipariş</span>
          <strong>{snapshot.orderNumber}</strong>
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
          <span className="display-kicker">ÖDENECEK TOPLAM</span>
          <strong>{formatMoney(snapshot.total, snapshot.currency)}</strong>
          <div className="display-total-breakdown">
            <div><span>Ara toplam</span><span>{formatMoney(snapshot.subtotal, snapshot.currency)}</span></div>
            {snapshot.discountTotal > 0 && (
              <div><span>İndirim</span><span>−{formatMoney(snapshot.discountTotal, snapshot.currency)}</span></div>
            )}
            <div><span>KDV</span><span>{formatMoney(snapshot.taxTotal, snapshot.currency)}</span></div>
          </div>
          <div className="display-message"><span>✓</span><p>{snapshot.message}</p></div>
          <small>Canlı güncelleme · v{snapshot.revision}</small>
        </aside>
      </section>
    </main>
  );
}

function DisplayBrand() {
  return <div className="display-brand"><span>A</span><strong>ALKAROS</strong></div>;
}

function ConnectionBanner() {
  return <div className="connection-banner">Bağlantı yenileniyor · güncel olmayan tutarlar otomatik temizlenecek</div>;
}
